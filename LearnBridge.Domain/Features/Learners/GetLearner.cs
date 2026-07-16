using LearnBridge.Domain.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LearnBridge.Domain.Features.Learners;

/// <summary>Fetches one learner profile with live consent state. Null → 404.</summary>
public sealed record GetLearnerQuery(Guid LearnerId) : IRequest<LearnerResponse?>;

internal sealed class GetLearnerQueryHandler : IRequestHandler<GetLearnerQuery, LearnerResponse?>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IAuditContext _auditContext;

    public GetLearnerQueryHandler(IApplicationDbContext dbContext, IAuditContext auditContext)
    {
        _dbContext = dbContext;
        _auditContext = auditContext;
    }

    public async Task<LearnerResponse?> Handle(GetLearnerQuery request, CancellationToken cancellationToken)
    {
        var learner = await _dbContext.Learners
            .Where(l => l.Id == request.LearnerId)
            .Select(l => new { l.Id, l.DisplayName, l.CreatedAt, l.AvatarConfig })
            .FirstOrDefaultAsync(cancellationToken);

        if (learner is null)
        {
            return null;
        }

        bool consentActive = await _dbContext.ParentalConsents
            .AnyAsync(c => c.LearnerId == learner.Id && c.RevokedAt == null, cancellationToken);

        _auditContext.MarkLearnerAccess(learner.Id, "learners");

        return new LearnerResponse(learner.Id, learner.DisplayName, learner.CreatedAt, consentActive, learner.AvatarConfig);
    }
}
