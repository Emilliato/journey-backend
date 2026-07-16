using LearnBridge.Api.Authorization;
using LearnBridge.Domain.Features.Sync;
using MediatR;
using Microsoft.AspNetCore.Authorization;

namespace LearnBridge.Api.Endpoints;

/// <summary>
/// The Sync API — see PLAN.md Phase 5. One batch upsert endpoint; the Angular
/// Sync Manager decides when to call it (on reconnect) and which pending Dexie
/// records to send. Thin: authorize, then dispatch.
/// </summary>
public static class SyncEndpoints
{
    public static void MapSyncEndpoints(this WebApplication app)
    {
        app.MapPost("/api/sync/batch", SyncBatchAsync)
            .WithTags("Sync")
            .WithName("SyncBatch")
            .RequireAuthorization();
    }

    private static async Task<IResult> SyncBatchAsync(
        SyncBatchRequest request,
        HttpContext httpContext,
        ISender sender,
        IAuthorizationService authorizationService)
    {
        AuthorizationResult authResult = await authorizationService.AuthorizeAsync(
            httpContext.User, new LearnerScopedResource(request.LearnerId), "LearnerDataAccess");

        if (!authResult.Succeeded)
        {
            return Results.Forbid();
        }

        SyncBatchOutcome outcome = await sender.Send(new SyncBatchCommand(request));

        return outcome.Status switch
        {
            SyncBatchOutcomeStatus.LearnerNotFound => Results.NotFound(),
            SyncBatchOutcomeStatus.ConsentInactive => Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["consent"] = ["Parental consent for this learner is not active."],
            }),
            _ => Results.Ok(outcome.Response),
        };
    }
}
