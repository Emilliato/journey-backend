using LearnBridge.Domain.Abstractions;
using LearnBridge.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LearnBridge.Domain.Features.Memory;

public sealed record MemoryDto(
    Guid Id,
    Guid? ConversationSessionId,
    string Category,
    string Content,
    DateTime CreatedAt,
    DateTime UpdatedAt);

/// <summary>
/// Read-side for the learner's memory repository (cached client-side for the
/// offline persona, and shown on the parent dashboard). Audits, does not
/// consent-gate. Null when the learner does not exist → 404.
/// </summary>
public sealed record ListMemoriesQuery(Guid LearnerId) : IRequest<IReadOnlyList<MemoryDto>?>;

internal sealed class ListMemoriesQueryHandler
    : IRequestHandler<ListMemoriesQuery, IReadOnlyList<MemoryDto>?>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IAuditContext _auditContext;

    public ListMemoriesQueryHandler(IApplicationDbContext dbContext, IAuditContext auditContext)
    {
        _dbContext = dbContext;
        _auditContext = auditContext;
    }

    public async Task<IReadOnlyList<MemoryDto>?> Handle(ListMemoriesQuery request, CancellationToken cancellationToken)
    {
        bool exists = await _dbContext.Learners.AnyAsync(l => l.Id == request.LearnerId, cancellationToken);

        if (!exists)
        {
            return null;
        }

        List<JourneyMemory> memories = await _dbContext.JourneyMemories
            .Where(m => m.LearnerId == request.LearnerId)
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync(cancellationToken);

        _auditContext.MarkLearnerAccess(request.LearnerId, "journey_memory");

        return memories
            .Select(m => new MemoryDto(
                m.Id,
                m.ConversationSessionId,
                ToCategoryString(m.Category),
                m.Content,
                m.CreatedAt,
                m.UpdatedAt))
            .ToList();
    }

    internal static string ToCategoryString(JourneyMemoryCategory category) => category switch
    {
        JourneyMemoryCategory.Academic => "academic",
        JourneyMemoryCategory.Preference => "preference",
        JourneyMemoryCategory.Engagement => "engagement",
        JourneyMemoryCategory.GoalRelated => "goal_related",
        _ => throw new InvalidOperationException($"Unhandled category '{category}'."),
    };
}
