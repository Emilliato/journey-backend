using LearnBridge.Api.Authorization;
using LearnBridge.Domain.Features.Learners;
using MediatR;
using Microsoft.AspNetCore.Authorization;

namespace LearnBridge.Api.Endpoints;

/// <summary>
/// Child profile management. Thin: each endpoint resolves the caller's
/// identity/authorization (a presentation concern) and dispatches a command
/// or query. Consent capture, the account/profile transaction, and audit
/// marking live in the handlers.
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
        CreateLearnerRequest request, HttpContext httpContext, ISender sender)
    {
        Guid? parentId = httpContext.User.GetParentId();

        if (parentId is null)
        {
            return Results.Unauthorized();
        }

        CreateLearnerResult result = await sender.Send(new CreateLearnerCommand(
            parentId.Value, request.DisplayName, request.Username, request.Password, request.ConsentGranted));

        return result.Status switch
        {
            CreateLearnerStatus.DisplayNameRequired => Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["displayName"] = ["Display name is required."],
            }),
            CreateLearnerStatus.CredentialsRequired => Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["username"] = ["A username and password for the learner are required."],
            }),
            CreateLearnerStatus.ConsentRequired => Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["consentGranted"] = ["Parental consent is required to create a learner profile."],
            }),
            CreateLearnerStatus.IdentityError => Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["identity"] = result.Errors?.ToArray() ?? [],
            }),
            _ => Results.Created($"/api/learners/{result.Learner!.Id}", result.Learner),
        };
    }

    private static async Task<IResult> ListLearnersAsync(HttpContext httpContext, ISender sender)
    {
        Guid? parentId = httpContext.User.GetParentId();

        if (parentId is null)
        {
            return Results.Unauthorized();
        }

        return Results.Ok(await sender.Send(new ListLearnersQuery(parentId.Value)));
    }

    private static async Task<IResult> GetLearnerAsync(
        Guid id, HttpContext httpContext, ISender sender, IAuthorizationService authorizationService)
    {
        AuthorizationResult authResult = await authorizationService.AuthorizeAsync(
            httpContext.User, new LearnerScopedResource(id), "LearnerDataAccess");

        if (!authResult.Succeeded)
        {
            return Results.Forbid();
        }

        LearnerResponse? learner = await sender.Send(new GetLearnerQuery(id));

        return learner is null ? Results.NotFound() : Results.Ok(learner);
    }

    private static async Task<IResult> UpdateAvatarAsync(
        Guid id,
        UpdateAvatarRequest request,
        HttpContext httpContext,
        ISender sender,
        IAuthorizationService authorizationService)
    {
        AuthorizationResult authResult = await authorizationService.AuthorizeAsync(
            httpContext.User, new LearnerScopedResource(id), "LearnerDataAccess");

        if (!authResult.Succeeded)
        {
            return Results.Forbid();
        }

        UpdateAvatarResult result = await sender.Send(new UpdateAvatarCommand(id, request.AvatarConfig));

        return result.Status switch
        {
            UpdateAvatarStatus.InvalidConfig => Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["avatarConfig"] = ["Avatar config is required, must be at most 4000 characters, and valid JSON."],
            }),
            UpdateAvatarStatus.NotFound => Results.NotFound(),
            _ => Results.Ok(result.Learner),
        };
    }

    private static async Task<IResult> SetConsentAsync(
        Guid id, SetConsentRequest request, HttpContext httpContext, ISender sender)
    {
        Guid? parentId = httpContext.User.GetParentId();
        bool isParent = httpContext.User.IsInRole(LearnBridgeRoles.Parent) ||
            httpContext.User.HasClaim(LearnBridgeClaimTypes.Role, LearnBridgeRoles.Parent);

        if (parentId is null || !isParent)
        {
            return Results.Forbid();
        }

        LearnerResponse? learner = await sender.Send(new SetConsentCommand(id, parentId.Value, request.Active));

        return learner is null ? Results.NotFound() : Results.Ok(learner);
    }
}
