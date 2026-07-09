using LearnBridge.Api.Auditing;
using LearnBridge.Data;
using LearnBridge.Data.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace LearnBridge.Api.Endpoints;

/// <summary>
/// Read-side for the Angular goal panel's initial load — live updates
/// during a conversation come back inline from
/// POST /api/journey/sessions/{id}/messages instead of a second round trip.
/// </summary>
public static class GoalEndpoints
{
    public static void MapGoalEndpoints(this WebApplication app)
    {
        app.MapGet("/api/learners/{learnerId:guid}/goals", ListGoalsAsync)
            .WithTags("Goals")
            .WithName("ListGoals")
            .RequireAuthorization();
    }

    private static async Task<IResult> ListGoalsAsync(
        Guid learnerId,
        HttpContext httpContext,
        LearnBridgeDbContext dbContext,
        IAuthorizationService authorizationService)
    {
        Learner? learner = await dbContext.Learners.FirstOrDefaultAsync(l => l.Id == learnerId);

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

        List<Goal> goals = await dbContext.Goals
            .Where(g => g.LearnerId == learnerId)
            .OrderByDescending(g => g.UpdatedAt)
            .ToListAsync();

        httpContext.MarkLearnerAccess(learnerId, "goals");

        var response = goals.Select(g => new
        {
            g.Id,
            g.Title,
            g.Description,
            Status = g.Status.ToString(),
            g.UpdatedAt,
        });

        return Results.Ok(response);
    }
}
