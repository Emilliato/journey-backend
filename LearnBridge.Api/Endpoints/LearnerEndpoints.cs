using LearnBridge.Api.Auditing;
using LearnBridge.Api.Authorization;
using LearnBridge.Api.Features.Learners;
using LearnBridge.Data;
using LearnBridge.Data.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace LearnBridge.Api.Endpoints;

/// <summary>
/// Child profile creation — online-only by construction (a plain
/// authenticated HTTP call, no offline queue). Consent is captured as part
/// of the same request that creates the learner: see CreateLearnerRequest
/// and CLAUDE.md constraint 2.
/// </summary>
public static class LearnerEndpoints
{
    public static void MapLearnerEndpoints(this WebApplication app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/learners").WithTags("Learners").RequireAuthorization();

        group.MapPost("/", CreateLearnerAsync).WithName("CreateLearner");
        group.MapGet("/", ListLearnersAsync).WithName("ListLearners");
        group.MapGet("/{id:guid}", GetLearnerAsync).WithName("GetLearner");
        group.MapPut("/{id:guid}/avatar", UpdateAvatarAsync).WithName("UpdateLearnerAvatar");
        group.MapPut("/{id:guid}/consent", SetConsentAsync).WithName("SetLearnerConsent");
    }

    private static async Task<IResult> CreateLearnerAsync(
        CreateLearnerRequest request,
        HttpContext httpContext,
        LearnBridgeDbContext dbContext,
        UserManager<ApplicationUser> userManager)
    {
        Guid? parentId = httpContext.User.GetParentId();

        if (parentId is null)
        {
            return Results.Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(request.DisplayName))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["displayName"] = ["Display name is required."],
            });
        }

        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["username"] = ["A username and password for the learner are required."],
            });
        }

        if (!request.ConsentGranted)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["consentGranted"] = ["Parental consent is required to create a learner profile."],
            });
        }

        // The learner's own sign-in (Learner role) — created first because
        // Identity persists immediately; if the profile save below fails,
        // the account is deleted again so no orphan sign-in remains.
        ApplicationUser learnerUser = new()
        {
            UserName = request.Username.Trim(),
            DisplayName = request.DisplayName,
        };

        IdentityResult identityResult = await userManager.CreateAsync(learnerUser, request.Password);

        if (!identityResult.Succeeded)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["identity"] = identityResult.Errors.Select(e => e.Description).ToArray(),
            });
        }

        await userManager.AddToRoleAsync(learnerUser, LearnBridgeRoles.Learner);

        Learner learner = new()
        {
            ParentId = parentId.Value,
            DisplayName = request.DisplayName,
            UserId = learnerUser.Id,
        };

        ParentalConsent consent = new()
        {
            LearnerId = learner.Id,
            ParentId = parentId.Value,
        };

        dbContext.Learners.Add(learner);
        dbContext.ParentalConsents.Add(consent);

        try
        {
            // Single SaveChangesAsync call — the learner and its founding
            // consent record land in one transaction, or neither does.
            await dbContext.SaveChangesAsync();
        }
        catch
        {
            await userManager.DeleteAsync(learnerUser);
            throw;
        }

        httpContext.MarkLearnerAccess(learner.Id, "learners");

        return Results.Created(
            $"/api/learners/{learner.Id}",
            new LearnerResponse(learner.Id, learner.DisplayName, learner.CreatedAt, consent.IsActive, learner.AvatarConfig));
    }

    private static async Task<IResult> ListLearnersAsync(HttpContext httpContext, LearnBridgeDbContext dbContext)
    {
        Guid? parentId = httpContext.User.GetParentId();

        if (parentId is null)
        {
            return Results.Unauthorized();
        }

        List<Learner> learners = await dbContext.Learners
            .Where(l => l.ParentId == parentId.Value)
            .OrderBy(l => l.CreatedAt)
            .ToListAsync();

        List<Guid> learnerIds = learners.Select(l => l.Id).ToList();

        HashSet<Guid> learnerIdsWithActiveConsent = (await dbContext.ParentalConsents
            .Where(c => learnerIds.Contains(c.LearnerId) && c.RevokedAt == null)
            .Select(c => c.LearnerId)
            .ToListAsync())
            .ToHashSet();

        foreach (Learner learner in learners)
        {
            httpContext.MarkLearnerAccess(learner.Id, "learners");
        }

        List<LearnerResponse> response = learners
            .Select(l => new LearnerResponse(l.Id, l.DisplayName, l.CreatedAt, learnerIdsWithActiveConsent.Contains(l.Id), l.AvatarConfig))
            .ToList();

        return Results.Ok(response);
    }

    private static async Task<IResult> GetLearnerAsync(
        Guid id,
        HttpContext httpContext,
        LearnBridgeDbContext dbContext,
        IAuthorizationService authorizationService)
    {
        Learner? learner = await dbContext.Learners.FirstOrDefaultAsync(l => l.Id == id);

        if (learner is null)
        {
            return Results.NotFound();
        }

        AuthorizationResult authResult = await authorizationService.AuthorizeAsync(
            httpContext.User, learner, "LearnerDataAccess");

        if (!authResult.Succeeded)
        {
            return Results.Forbid();
        }

        bool consentActive = await dbContext.ParentalConsents
            .AnyAsync(c => c.LearnerId == learner.Id && c.RevokedAt == null);

        httpContext.MarkLearnerAccess(learner.Id, "learners");

        return Results.Ok(new LearnerResponse(learner.Id, learner.DisplayName, learner.CreatedAt, consentActive, learner.AvatarConfig));
    }

    /// <summary>
    /// Avatar Studio save. The config is cosmetic self-expression (not
    /// learning data, so no consent gate — see Learner.AvatarConfig), and a
    /// learner may update their own; the write still audits.
    /// </summary>
    private static async Task<IResult> UpdateAvatarAsync(
        Guid id,
        UpdateAvatarRequest request,
        HttpContext httpContext,
        LearnBridgeDbContext dbContext,
        IAuthorizationService authorizationService)
    {
        if (string.IsNullOrWhiteSpace(request.AvatarConfig) || request.AvatarConfig.Length > 4000)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["avatarConfig"] = ["Avatar config is required and must be at most 4000 characters."],
            });
        }

        try
        {
            using System.Text.Json.JsonDocument _ = System.Text.Json.JsonDocument.Parse(request.AvatarConfig);
        }
        catch (System.Text.Json.JsonException)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["avatarConfig"] = ["Avatar config must be valid JSON."],
            });
        }

        Learner? learner = await dbContext.Learners.FirstOrDefaultAsync(l => l.Id == id);

        if (learner is null)
        {
            return Results.NotFound();
        }

        AuthorizationResult authResult = await authorizationService.AuthorizeAsync(
            httpContext.User, learner, "LearnerDataAccess");

        if (!authResult.Succeeded)
        {
            return Results.Forbid();
        }

        learner.AvatarConfig = request.AvatarConfig;
        learner.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync();

        httpContext.MarkLearnerAccess(learner.Id, "learners");

        bool consentActive = await dbContext.ParentalConsents
            .AnyAsync(c => c.LearnerId == learner.Id && c.RevokedAt == null);

        return Results.Ok(new LearnerResponse(learner.Id, learner.DisplayName, learner.CreatedAt, consentActive, learner.AvatarConfig));
    }

    /// <summary>
    /// Parent dashboard consent toggle — parent-only (a learner must not be
    /// able to grant their own consent). Revoking soft-deletes (RevokedAt)
    /// so the consent history stays auditable; granting adds a fresh row.
    /// The server-side write gates elsewhere read live state, so this takes
    /// effect immediately (constraint 2).
    /// </summary>
    private static async Task<IResult> SetConsentAsync(
        Guid id,
        SetConsentRequest request,
        HttpContext httpContext,
        LearnBridgeDbContext dbContext)
    {
        Guid? parentId = httpContext.User.GetParentId();
        bool isParent = httpContext.User.IsInRole(LearnBridgeRoles.Parent) ||
            httpContext.User.HasClaim(LearnBridgeClaimTypes.Role, LearnBridgeRoles.Parent);

        if (parentId is null || !isParent)
        {
            return Results.Forbid();
        }

        Learner? learner = await dbContext.Learners
            .FirstOrDefaultAsync(l => l.Id == id && l.ParentId == parentId.Value);

        if (learner is null)
        {
            return Results.NotFound();
        }

        List<ParentalConsent> activeConsents = await dbContext.ParentalConsents
            .Where(c => c.LearnerId == learner.Id && c.RevokedAt == null)
            .ToListAsync();

        if (request.Active && activeConsents.Count == 0)
        {
            dbContext.ParentalConsents.Add(new ParentalConsent
            {
                LearnerId = learner.Id,
                ParentId = parentId.Value,
            });
        }
        else if (!request.Active)
        {
            foreach (ParentalConsent consent in activeConsents)
            {
                consent.RevokedAt = DateTime.UtcNow;
            }
        }

        await dbContext.SaveChangesAsync();

        httpContext.MarkLearnerAccess(learner.Id, "parental_consent");

        return Results.Ok(new LearnerResponse(learner.Id, learner.DisplayName, learner.CreatedAt, request.Active, learner.AvatarConfig));
    }
}
