using LearnBridge.Api.Authorization;
using LearnBridge.Domain.Features.Dashboard;
using MediatR;
using Microsoft.AspNetCore.Authorization;

namespace LearnBridge.Api.Endpoints;

/// <summary>
/// Presentation for the parent dashboard aggregate. Thin: it enforces the
/// LearnerDataAccess policy on the learner id, then dispatches the query to
/// the application layer (see <see cref="GetDashboardQuery"/>). The audit
/// marking and data access live in the handler.
/// </summary>
public static class DashboardEndpoints
{
    public static void MapDashboardEndpoints(this WebApplication app)
    {
        app.MapGet("/api/learners/{learnerId:guid}/dashboard", GetDashboardAsync)
            .WithTags("Dashboard")
            .WithName("GetLearnerDashboard")
            .RequireAuthorization();
    }

    private static async Task<IResult> GetDashboardAsync(
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

        DashboardResponse? response = await sender.Send(new GetDashboardQuery(learnerId));

        return response is null ? Results.NotFound() : Results.Ok(response);
    }
}
