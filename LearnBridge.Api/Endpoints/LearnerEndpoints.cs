using LearnBridge.Api.Auditing;
using LearnBridge.Api.Authorization;
using LearnBridge.Api.Features.Learners;
using LearnBridge.Data;
using LearnBridge.Data.Entities;
using Microsoft.AspNetCore.Authorization;
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
    }

    private static async Task<IResult> CreateLearnerAsync(
        CreateLearnerRequest request,
        HttpContext httpContext,
        LearnBridgeDbContext dbContext)
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

        if (!request.ConsentGranted)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["consentGranted"] = ["Parental consent is required to create a learner profile."],
            });
        }

        Learner learner = new()
        {
            ParentId = parentId.Value,
            DisplayName = request.DisplayName,
        };

        ParentalConsent consent = new()
        {
            LearnerId = learner.Id,
            ParentId = parentId.Value,
        };

        dbContext.Learners.Add(learner);
        dbContext.ParentalConsents.Add(consent);

        // Single SaveChangesAsync call — the learner and its founding
        // consent record land in one transaction, or neither does.
        await dbContext.SaveChangesAsync();

        httpContext.MarkLearnerAccess(learner.Id, "learners");

        return Results.Created(
            $"/api/learners/{learner.Id}",
            new LearnerResponse(learner.Id, learner.DisplayName, learner.CreatedAt, consent.IsActive));
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
            .Select(l => new LearnerResponse(l.Id, l.DisplayName, l.CreatedAt, learnerIdsWithActiveConsent.Contains(l.Id)))
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

        return Results.Ok(new LearnerResponse(learner.Id, learner.DisplayName, learner.CreatedAt, consentActive));
    }
}
