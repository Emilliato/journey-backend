using LearnBridge.Domain.Abstractions;
using LearnBridge.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LearnBridge.Domain.Features.Memory;

/// <summary>
/// The parent dashboard's per-memory "remove" affordance. Parent-only by
/// construction: <paramref name="RequesterId"/> is the authenticated parent's
/// id and must own the learner, so a learner token can never delete what a
/// parent reviews. Hard delete (the affordance's point), audited (constraint 5).
/// Returns false when the learner/parent or memory is not found.
/// </summary>
public sealed record DeleteMemoryCommand(Guid LearnerId, Guid MemoryId, Guid RequesterId) : IRequest<bool>;

internal sealed class DeleteMemoryCommandHandler : IRequestHandler<DeleteMemoryCommand, bool>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IAuditContext _auditContext;

    public DeleteMemoryCommandHandler(IApplicationDbContext dbContext, IAuditContext auditContext)
    {
        _dbContext = dbContext;
        _auditContext = auditContext;
    }

    public async Task<bool> Handle(DeleteMemoryCommand request, CancellationToken cancellationToken)
    {
        bool ownsLearner = await _dbContext.Learners
            .AnyAsync(l => l.Id == request.LearnerId && l.ParentId == request.RequesterId, cancellationToken);

        if (!ownsLearner)
        {
            return false;
        }

        JourneyMemory? memory = await _dbContext.JourneyMemories
            .FirstOrDefaultAsync(m => m.Id == request.MemoryId && m.LearnerId == request.LearnerId, cancellationToken);

        if (memory is null)
        {
            return false;
        }

        _dbContext.JourneyMemories.Remove(memory);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _auditContext.MarkLearnerAccess(request.LearnerId, "journey_memory");

        return true;
    }
}
