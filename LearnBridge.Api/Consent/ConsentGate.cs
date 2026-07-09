using LearnBridge.Data;
using Microsoft.EntityFrameworkCore;

namespace LearnBridge.Api.Consent;

/// <summary>
/// Server-side enforcement for CLAUDE.md constraint 2: no write to
/// learning_profile, goals, or journey_memory without an active (non-
/// revoked) parental_consent record. Call this immediately before any such
/// write — do not cache the result across requests.
/// </summary>
public sealed class ConsentGate
{
    private readonly LearnBridgeDbContext _dbContext;

    public ConsentGate(LearnBridgeDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<bool> IsActiveAsync(Guid learnerId, CancellationToken cancellationToken = default)
    {
        return _dbContext.ParentalConsents
            .AnyAsync(c => c.LearnerId == learnerId && c.RevokedAt == null, cancellationToken);
    }
}
