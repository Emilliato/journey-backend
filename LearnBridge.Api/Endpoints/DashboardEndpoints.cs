using LearnBridge.Api.Auditing;
using LearnBridge.Api.Features.Dashboard;
using LearnBridge.Data;
using LearnBridge.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace LearnBridge.Api.Endpoints;

/// <summary>
/// Aggregated read-side for the parent dashboard and the learner home's
/// streak/badges strip. Reads several learner-linked tables in one request,
/// so it marks one audit access per resource touched (constraint 5). Like
/// the other read endpoints it audits but does not consent-gate — consent
/// gates writes (constraint 2).
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

        DateTime utcNow = DateTime.UtcNow;
        DateTime windowStart = utcNow.AddDays(-90);

        List<ConversationSession> sessions = await dbContext.ConversationSessions
            .Where(s => s.LearnerId == learnerId && s.StartedAt >= windowStart)
            .ToListAsync();

        List<Goal> goals = await dbContext.Goals
            .Where(g => g.LearnerId == learnerId)
            .ToListAsync();

        List<JourneyMemory> memories = await dbContext.JourneyMemories
            .Where(m => m.LearnerId == learnerId)
            .ToListAsync();

        httpContext.MarkLearnerAccess(learnerId, "conversation_sessions");
        httpContext.MarkLearnerAccess(learnerId, "goals");
        httpContext.MarkLearnerAccess(learnerId, "journey_memory");

        return Results.Ok(new DashboardResponse(
            DashboardCalculator.Compute(utcNow, sessions, goals, memories),
            DashboardCalculator.BuildTimeline(utcNow, sessions, goals, memories)));
    }
}
