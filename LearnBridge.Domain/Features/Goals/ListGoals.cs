using LearnBridge.Domain.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LearnBridge.Domain.Features.Goals;

public sealed record GoalListItemDto(Guid Id, string Title, string? Description, string Status, DateTime UpdatedAt);

/// <summary>
/// Read-side for the Angular goal panel's initial load. Audits (constraint 5),
/// does not consent-gate (reads are open, constraint 2). Null when the learner
/// does not exist → 404 at the endpoint.
/// </summary>
public sealed record ListGoalsQuery(Guid LearnerId) : IRequest<IReadOnlyList<GoalListItemDto>?>;

internal sealed class ListGoalsQueryHandler
    : IRequestHandler<ListGoalsQuery, IReadOnlyList<GoalListItemDto>?>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IAuditContext _auditContext;

    public ListGoalsQueryHandler(IApplicationDbContext dbContext, IAuditContext auditContext)
    {
        _dbContext = dbContext;
        _auditContext = auditContext;
    }

    public async Task<IReadOnlyList<GoalListItemDto>?> Handle(ListGoalsQuery request, CancellationToken cancellationToken)
    {
        bool exists = await _dbContext.Learners.AnyAsync(l => l.Id == request.LearnerId, cancellationToken);

        if (!exists)
        {
            return null;
        }

        List<GoalListItemDto> goals = await _dbContext.Goals
            .Where(g => g.LearnerId == request.LearnerId)
            .OrderByDescending(g => g.UpdatedAt)
            .Select(g => new GoalListItemDto(g.Id, g.Title, g.Description, g.Status.ToString(), g.UpdatedAt))
            .ToListAsync(cancellationToken);

        _auditContext.MarkLearnerAccess(request.LearnerId, "goals");

        return goals;
    }
}
