using LearnBridge.Api.Auditing;
using LearnBridge.Api.Consent;
using LearnBridge.Data;
using LearnBridge.Domain.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace LearnBridge.Api.Features.Sync;

public enum SyncBatchStatus
{
    Applied,
    ConsentInactive,
}

public sealed class SyncBatchResult
{
    public required SyncBatchStatus Status { get; init; }

    public SyncBatchResponse? Response { get; init; }
}

/// <summary>
/// Reconciles a client's queued offline writes against the server, one
/// record at a time, last-write-wins by <c>UpdatedAt</c> — the default per
/// CLAUDE.md constraint 6 (safe only under the single-learner,
/// single-primary-device assumption noted there). The response always
/// carries the authoritative post-resolution state for every submitted
/// record, whichever side won, so the Angular Sync Manager can overwrite
/// its local copy unconditionally and clear pendingSync.
/// </summary>
public sealed class SyncService
{
    private readonly LearnBridgeDbContext _dbContext;
    private readonly ConsentGate _consentGate;
    private readonly HttpContext _httpContext;

    public SyncService(LearnBridgeDbContext dbContext, ConsentGate consentGate, IHttpContextAccessor httpContextAccessor)
    {
        _dbContext = dbContext;
        _consentGate = consentGate;
        _httpContext = httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("SyncService requires an active HTTP context.");
    }

    public async Task<SyncBatchResult> ApplyBatchAsync(Guid learnerId, SyncBatchRequest request, CancellationToken cancellationToken)
    {
        if (!await _consentGate.IsActiveAsync(learnerId, cancellationToken))
        {
            return new SyncBatchResult { Status = SyncBatchStatus.ConsentInactive };
        }

        List<SyncGoalDto> resolvedGoals = [];

        foreach (SyncGoalDto incoming in request.Goals)
        {
            SyncGoalDto? resolved = await ApplyGoalAsync(learnerId, incoming, cancellationToken);

            if (resolved is not null)
            {
                resolvedGoals.Add(resolved);
            }
        }

        List<SyncJourneyMemoryDto> resolvedMemories = [];

        foreach (SyncJourneyMemoryDto incoming in request.JourneyMemories)
        {
            SyncJourneyMemoryDto? resolved = await ApplyJourneyMemoryAsync(learnerId, incoming, cancellationToken);

            if (resolved is not null)
            {
                resolvedMemories.Add(resolved);
            }
        }

        if (request.Goals.Count > 0)
        {
            _httpContext.MarkLearnerAccess(learnerId, "goals");
        }

        if (request.JourneyMemories.Count > 0)
        {
            _httpContext.MarkLearnerAccess(learnerId, "journey_memory");
        }

        return new SyncBatchResult
        {
            Status = SyncBatchStatus.Applied,
            Response = new SyncBatchResponse(resolvedGoals, resolvedMemories),
        };
    }

    private async Task<SyncGoalDto?> ApplyGoalAsync(Guid learnerId, SyncGoalDto incoming, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(incoming.Title) ||
            !ClosedEnumParsing.TryParseGoalStatus(incoming.Status, out GoalStatus incomingStatus))
        {
            return null;
        }

        Goal? existing = await _dbContext.Goals.FirstOrDefaultAsync(g => g.Id == incoming.Id, cancellationToken);

        if (existing is not null && existing.LearnerId != learnerId)
        {
            // Id collision with a row belonging to a different learner —
            // refuse rather than risk a primary-key clash on insert.
            return null;
        }

        if (existing is null)
        {
            Goal created = new()
            {
                Id = incoming.Id,
                LearnerId = learnerId,
                Title = incoming.Title,
                Description = incoming.Description,
                Status = incomingStatus,
                UpdatedAt = incoming.UpdatedAt,
            };

            _dbContext.Goals.Add(created);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return ToDto(created);
        }

        if (incoming.UpdatedAt > existing.UpdatedAt)
        {
            existing.Title = incoming.Title;
            existing.Description = incoming.Description;
            existing.Status = incomingStatus;
            existing.UpdatedAt = incoming.UpdatedAt;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return ToDto(existing);
    }

    private async Task<SyncJourneyMemoryDto?> ApplyJourneyMemoryAsync(
        Guid learnerId, SyncJourneyMemoryDto incoming, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(incoming.Content) ||
            !ClosedEnumParsing.TryParseJourneyMemoryCategory(incoming.Category, out JourneyMemoryCategory incomingCategory))
        {
            return null;
        }

        JourneyMemory? existing = await _dbContext.JourneyMemories
            .FirstOrDefaultAsync(m => m.Id == incoming.Id, cancellationToken);

        if (existing is not null && existing.LearnerId != learnerId)
        {
            return null;
        }

        if (existing is null)
        {
            JourneyMemory created = new()
            {
                Id = incoming.Id,
                LearnerId = learnerId,
                // Offline-created memories have no server-side conversation
                // session (Phase 4's offline path never creates one), so a
                // client-submitted session id that doesn't exist here would
                // violate the FK — only trust it if it's null.
                ConversationSessionId = null,
                Category = incomingCategory,
                Content = incoming.Content,
                CreatedAt = incoming.CreatedAt,
                UpdatedAt = incoming.UpdatedAt,
            };

            _dbContext.JourneyMemories.Add(created);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return ToDto(created);
        }

        if (incoming.UpdatedAt > existing.UpdatedAt)
        {
            existing.Category = incomingCategory;
            existing.Content = incoming.Content;
            existing.UpdatedAt = incoming.UpdatedAt;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return ToDto(existing);
    }

    private static SyncGoalDto ToDto(Goal goal) =>
        // PascalCase, matching GoalUpdateDto and the Angular Goal/GoalUpdate
        // models already shipped in Phases 2-3 — see ClosedEnumParsing for
        // why parsing accepts either casing.
        new(goal.Id, goal.Title, goal.Description, goal.Status.ToString(), goal.UpdatedAt);

    private static SyncJourneyMemoryDto ToDto(JourneyMemory memory) =>
        new(
            memory.Id,
            memory.ConversationSessionId,
            memory.Category switch
            {
                JourneyMemoryCategory.Academic => "academic",
                JourneyMemoryCategory.Preference => "preference",
                JourneyMemoryCategory.Engagement => "engagement",
                JourneyMemoryCategory.GoalRelated => "goal_related",
                _ => throw new InvalidOperationException($"Unhandled category '{memory.Category}'."),
            },
            memory.Content,
            memory.CreatedAt,
            memory.UpdatedAt);
}
