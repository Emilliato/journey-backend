using LearnBridge.Api.Auditing;
using LearnBridge.Data;
using LearnBridge.Data.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace LearnBridge.Api.Endpoints;

/// <summary>
/// Read-side for the learner's memory repository. Exists so the Angular
/// client can cache what JOURNEY already knows about a learner while online
/// (see OfflineCacheService), giving the offline persona the same
/// personalised context the online system prompt gets. Like the goal read
/// endpoint, this is a learner-linked read: it audits (constraint 5) but
/// does not consent-gate — consent gates writes, not reads (constraint 2).
/// </summary>
public static class MemoryEndpoints
{
    public static void MapMemoryEndpoints(this WebApplication app)
    {
        app.MapGet("/api/learners/{learnerId:guid}/memories", ListMemoriesAsync)
            .WithTags("Memories")
            .WithName("ListMemories")
            .RequireAuthorization();
    }

    private static async Task<IResult> ListMemoriesAsync(
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

        List<JourneyMemory> memories = await dbContext.JourneyMemories
            .Where(m => m.LearnerId == learnerId)
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync();

        httpContext.MarkLearnerAccess(learnerId, "journey_memory");

        var response = memories.Select(m => new
        {
            m.Id,
            m.ConversationSessionId,
            Category = ToCategoryString(m.Category),
            m.Content,
            m.CreatedAt,
            m.UpdatedAt,
        });

        return Results.Ok(response);
    }

    private static string ToCategoryString(JourneyMemoryCategory category) => category switch
    {
        JourneyMemoryCategory.Academic => "academic",
        JourneyMemoryCategory.Preference => "preference",
        JourneyMemoryCategory.Engagement => "engagement",
        JourneyMemoryCategory.GoalRelated => "goal_related",
        _ => throw new InvalidOperationException($"Unhandled category '{category}'."),
    };
}
