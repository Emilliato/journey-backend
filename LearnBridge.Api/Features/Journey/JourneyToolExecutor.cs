using System.Text.Json.Nodes;
using LearnBridge.Api.Auditing;
using LearnBridge.Api.Consent;
using LearnBridge.Data;
using LearnBridge.Data.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace LearnBridge.Api.Features.Journey;

public sealed class ToolExecutionResult
{
    public required string ResultText { get; init; }

    public GoalUpdateDto? GoalUpdate { get; init; }

    public bool MemoryRecorded { get; init; }
}

/// <summary>
/// Applies the DB side effects of a tool call JOURNEY made mid-conversation.
/// Every write here goes through <see cref="ConsentGate"/> first (CLAUDE.md
/// constraint 2) and is marked for the audit middleware (constraint 5).
/// </summary>
public sealed class JourneyToolExecutor
{
    private readonly LearnBridgeDbContext _dbContext;
    private readonly ConsentGate _consentGate;
    private readonly HttpContext _httpContext;

    public JourneyToolExecutor(LearnBridgeDbContext dbContext, ConsentGate consentGate, IHttpContextAccessor httpContextAccessor)
    {
        _dbContext = dbContext;
        _consentGate = consentGate;
        _httpContext = httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("JourneyToolExecutor requires an active HTTP context.");
    }

    public async Task<ToolExecutionResult> ExecuteAsync(
        Guid learnerId,
        Guid conversationSessionId,
        string toolName,
        JsonObject? input,
        CancellationToken cancellationToken)
    {
        if (!await _consentGate.IsActiveAsync(learnerId, cancellationToken))
        {
            return new ToolExecutionResult
            {
                ResultText = "Not saved: parental consent for this learner is not currently active.",
            };
        }

        return toolName switch
        {
            JourneyTools.RecordMemory => await ExecuteRecordMemoryAsync(learnerId, conversationSessionId, input, cancellationToken),
            JourneyTools.UpdateGoal => await ExecuteUpdateGoalAsync(learnerId, input, cancellationToken),
            _ => new ToolExecutionResult { ResultText = $"Unknown tool '{toolName}' — no action taken." },
        };
    }

    private async Task<ToolExecutionResult> ExecuteRecordMemoryAsync(
        Guid learnerId, Guid conversationSessionId, JsonObject? input, CancellationToken cancellationToken)
    {
        string? categoryRaw = input?["category"]?.GetValue<string>();
        string? content = input?["content"]?.GetValue<string>();

        if (string.IsNullOrWhiteSpace(content) || !ClosedEnumParsing.TryParseJourneyMemoryCategory(categoryRaw, out JourneyMemoryCategory category))
        {
            return new ToolExecutionResult
            {
                ResultText = "Not saved: category must be one of academic, preference, engagement, goal_related, and content is required.",
            };
        }

        JourneyMemory memory = new()
        {
            LearnerId = learnerId,
            ConversationSessionId = conversationSessionId,
            Category = category,
            Content = content,
        };

        _dbContext.JourneyMemories.Add(memory);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _httpContext.MarkLearnerAccess(learnerId, "journey_memory");

        return new ToolExecutionResult { ResultText = "Saved.", MemoryRecorded = true };
    }

    private async Task<ToolExecutionResult> ExecuteUpdateGoalAsync(
        Guid learnerId, JsonObject? input, CancellationToken cancellationToken)
    {
        string? title = input?["title"]?.GetValue<string>();
        string? statusRaw = input?["status"]?.GetValue<string>();
        string? description = input?["description"]?.GetValue<string>();

        if (string.IsNullOrWhiteSpace(title) || !ClosedEnumParsing.TryParseGoalStatus(statusRaw, out GoalStatus status))
        {
            return new ToolExecutionResult
            {
                ResultText = "Not saved: title is required and status must be one of active, completed, abandoned.",
            };
        }

        Goal? existing = await _dbContext.Goals
            .FirstOrDefaultAsync(g => g.LearnerId == learnerId && g.Title == title, cancellationToken);

        bool wasCreated = existing is null;
        Goal goal = existing ?? new Goal { LearnerId = learnerId, Title = title };

        goal.Status = status;
        goal.UpdatedAt = DateTime.UtcNow;

        if (description is not null)
        {
            goal.Description = description;
        }

        if (wasCreated)
        {
            _dbContext.Goals.Add(goal);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        _httpContext.MarkLearnerAccess(learnerId, "goals");

        GoalUpdateDto dto = new(goal.Id, goal.Title, goal.Description, goal.Status.ToString(), wasCreated);

        return new ToolExecutionResult
        {
            ResultText = wasCreated ? "Goal created." : "Goal updated.",
            GoalUpdate = dto,
        };
    }

}
