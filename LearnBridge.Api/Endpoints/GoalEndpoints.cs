using LearnBridge.Api.Authorization;
using LearnBridge.Domain.Features.Goals;
using MediatR;
using Microsoft.AspNetCore.Authorization;

namespace LearnBridge.Api.Endpoints;

/// <summary>
/// Presentation for the goal read-side. Thin: enforces the LearnerDataAccess
/// policy, then dispatches <see cref="ListGoalsQuery"/>.
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
        ISender sender,
        IAuthorizationService authorizationService)
    {
        AuthorizationResult authResult = await authorizationService.AuthorizeAsync(
            httpContext.User, new LearnerScopedResource(learnerId), "LearnerDataAccess");

        if (!authResult.Succeeded)
        {
            return Results.Forbid();
        }

        IReadOnlyList<GoalListItemDto>? goals = await sender.Send(new ListGoalsQuery(learnerId));

        return goals is null ? Results.NotFound() : Results.Ok(goals);
    }
}
