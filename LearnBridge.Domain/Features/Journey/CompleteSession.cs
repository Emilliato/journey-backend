using LearnBridge.Domain.Abstractions;
using LearnBridge.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LearnBridge.Domain.Features.Journey;

/// <summary>
/// Ends a session: stamps the ConversationSession's EndedAt and drops the
/// in-memory transcript. The session must belong to the calling parent.
/// Returns false when the session isn't found → 404.
/// </summary>
public sealed record CompleteSessionCommand(Guid SessionId, Guid ParentId) : IRequest<bool>;

internal sealed class CompleteSessionCommandHandler : IRequestHandler<CompleteSessionCommand, bool>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IJourneySessionStore _sessionStore;
    private readonly IAuditContext _auditContext;

    public CompleteSessionCommandHandler(
        IApplicationDbContext dbContext, IJourneySessionStore sessionStore, IAuditContext auditContext)
    {
        _dbContext = dbContext;
        _sessionStore = sessionStore;
        _auditContext = auditContext;
    }

    public async Task<bool> Handle(CompleteSessionCommand request, CancellationToken cancellationToken)
    {
        JourneySessionState? session = _sessionStore.Get(request.SessionId);

        if (session is null || session.ParentId != request.ParentId)
        {
            return false;
        }

        ConversationSession? dbSession = await _dbContext.ConversationSessions
            .FirstOrDefaultAsync(s => s.Id == request.SessionId, cancellationToken);

        if (dbSession is not null)
        {
            dbSession.EndedAt = DateTime.UtcNow;
            dbSession.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        _sessionStore.Complete(request.SessionId);

        _auditContext.MarkLearnerAccess(session.LearnerId, "conversation_sessions");

        return true;
    }
}
