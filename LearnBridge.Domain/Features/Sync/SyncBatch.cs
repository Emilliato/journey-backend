using LearnBridge.Domain.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LearnBridge.Domain.Features.Sync;

public enum SyncBatchOutcomeStatus
{
    LearnerNotFound,
    ConsentInactive,
    Applied,
}

public sealed record SyncBatchOutcome(SyncBatchOutcomeStatus Status, SyncBatchResponse? Response = null);

/// <summary>
/// Applies a client's queued offline writes (last-write-wins). Delegates the
/// per-record reconciliation to <see cref="SyncService"/>, which consent-gates
/// (constraint 2) and audits (constraint 5). Authorization is enforced in the
/// endpoint; existence is checked here (null learner → 404).
/// </summary>
public sealed record SyncBatchCommand(SyncBatchRequest Request) : IRequest<SyncBatchOutcome>;

internal sealed class SyncBatchCommandHandler : IRequestHandler<SyncBatchCommand, SyncBatchOutcome>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly SyncService _syncService;

    public SyncBatchCommandHandler(IApplicationDbContext dbContext, SyncService syncService)
    {
        _dbContext = dbContext;
        _syncService = syncService;
    }

    public async Task<SyncBatchOutcome> Handle(SyncBatchCommand request, CancellationToken cancellationToken)
    {
        Guid learnerId = request.Request.LearnerId;

        bool exists = await _dbContext.Learners.AnyAsync(l => l.Id == learnerId, cancellationToken);

        if (!exists)
        {
            return new SyncBatchOutcome(SyncBatchOutcomeStatus.LearnerNotFound);
        }

        SyncBatchResult result = await _syncService.ApplyBatchAsync(learnerId, request.Request, cancellationToken);

        return result.Status == SyncBatchStatus.ConsentInactive
            ? new SyncBatchOutcome(SyncBatchOutcomeStatus.ConsentInactive)
            : new SyncBatchOutcome(SyncBatchOutcomeStatus.Applied, result.Response);
    }
}
