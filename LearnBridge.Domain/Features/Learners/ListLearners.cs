using LearnBridge.Domain.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LearnBridge.Domain.Features.Learners;

/// <summary>Lists a parent's children, each with active-consent state and avatar.</summary>
public sealed record ListLearnersQuery(Guid ParentId) : IRequest<IReadOnlyList<LearnerResponse>>;

internal sealed class ListLearnersQueryHandler
    : IRequestHandler<ListLearnersQuery, IReadOnlyList<LearnerResponse>>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IAuditContext _auditContext;

    public ListLearnersQueryHandler(IApplicationDbContext dbContext, IAuditContext auditContext)
    {
        _dbContext = dbContext;
        _auditContext = auditContext;
    }

    public async Task<IReadOnlyList<LearnerResponse>> Handle(ListLearnersQuery request, CancellationToken cancellationToken)
    {
        var learners = await _dbContext.Learners
            .Where(l => l.ParentId == request.ParentId)
            .OrderBy(l => l.CreatedAt)
            .Select(l => new { l.Id, l.DisplayName, l.CreatedAt, l.AvatarConfig })
            .ToListAsync(cancellationToken);

        List<Guid> learnerIds = learners.Select(l => l.Id).ToList();

        HashSet<Guid> withActiveConsent = (await _dbContext.ParentalConsents
            .Where(c => learnerIds.Contains(c.LearnerId) && c.RevokedAt == null)
            .Select(c => c.LearnerId)
            .ToListAsync(cancellationToken))
            .ToHashSet();

        foreach (Guid learnerId in learnerIds)
        {
            _auditContext.MarkLearnerAccess(learnerId, "learners");
        }

        return learners
            .Select(l => new LearnerResponse(
                l.Id, l.DisplayName, l.CreatedAt, withActiveConsent.Contains(l.Id), l.AvatarConfig))
            .ToList();
    }
}
