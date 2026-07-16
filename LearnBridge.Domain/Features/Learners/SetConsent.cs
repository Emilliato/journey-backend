using LearnBridge.Domain.Abstractions;
using LearnBridge.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LearnBridge.Domain.Features.Learners;

/// <summary>
/// Parent dashboard consent toggle. Parent-only: <paramref name="ParentId"/>
/// must own the learner (the endpoint also checks the caller is a parent).
/// Revoking soft-deletes (RevokedAt) so the consent history stays auditable;
/// granting adds a fresh row. Takes effect immediately — the write gates read
/// live state (constraint 2). Null → 404.
/// </summary>
public sealed record SetConsentCommand(Guid LearnerId, Guid ParentId, bool Active) : IRequest<LearnerResponse?>;

internal sealed class SetConsentCommandHandler : IRequestHandler<SetConsentCommand, LearnerResponse?>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IAuditContext _auditContext;

    public SetConsentCommandHandler(IApplicationDbContext dbContext, IAuditContext auditContext)
    {
        _dbContext = dbContext;
        _auditContext = auditContext;
    }

    public async Task<LearnerResponse?> Handle(SetConsentCommand request, CancellationToken cancellationToken)
    {
        Learner? learner = await _dbContext.Learners
            .FirstOrDefaultAsync(l => l.Id == request.LearnerId && l.ParentId == request.ParentId, cancellationToken);

        if (learner is null)
        {
            return null;
        }

        List<ParentalConsent> activeConsents = await _dbContext.ParentalConsents
            .Where(c => c.LearnerId == learner.Id && c.RevokedAt == null)
            .ToListAsync(cancellationToken);

        if (request.Active && activeConsents.Count == 0)
        {
            _dbContext.ParentalConsents.Add(new ParentalConsent
            {
                LearnerId = learner.Id,
                ParentId = request.ParentId,
            });
        }
        else if (!request.Active)
        {
            foreach (ParentalConsent consent in activeConsents)
            {
                consent.RevokedAt = DateTime.UtcNow;
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        _auditContext.MarkLearnerAccess(learner.Id, "parental_consent");

        return new LearnerResponse(learner.Id, learner.DisplayName, learner.CreatedAt, request.Active, learner.AvatarConfig);
    }
}
