using LearnBridge.Api.Features.Sync;
using LearnBridge.Data;
using LearnBridge.Data.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace LearnBridge.Api.Endpoints;

/// <summary>
/// The Sync API — see PLAN.md Phase 5. One batch upsert endpoint; the
/// Angular Sync Manager decides when to call it (on reconnect) and which
/// pending Dexie records to send.
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
        LearnBridgeDbContext dbContext,
        SyncService syncService,
        IAuthorizationService authorizationService)
    {
        Learner? learner = await dbContext.Learners.FirstOrDefaultAsync(l => l.Id == request.LearnerId);

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

        SyncBatchResult result = await syncService.ApplyBatchAsync(request.LearnerId, request, httpContext.RequestAborted);

        if (result.Status == SyncBatchStatus.ConsentInactive)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["consent"] = ["Parental consent for this learner is not active."],
            });
        }

        return Results.Ok(result.Response);
    }
}
