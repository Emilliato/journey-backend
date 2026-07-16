using LearnBridge.Domain.Abstractions;
using MediatR;

namespace LearnBridge.Domain.Features.Journey;

public enum SendMessageStatus
{
    SessionNotFound,
    MessageRequired,
    Replied,
}

public sealed record SendMessageOutcome(SendMessageStatus Status, SendMessageResponse? Response = null);

/// <summary>
/// Runs one learner message through JOURNEY (Claude + any tool calls). The
/// session is looked up in the in-memory store and must belong to the calling
/// parent. Audited (constraint 5); writes inside the tool loop are consent-gated.
/// </summary>
public sealed record SendMessageCommand(Guid SessionId, Guid ParentId, string Message)
    : IRequest<SendMessageOutcome>;

internal sealed class SendMessageCommandHandler : IRequestHandler<SendMessageCommand, SendMessageOutcome>
{
    private readonly IJourneySessionStore _sessionStore;
    private readonly JourneyConversationService _conversationService;
    private readonly IAuditContext _auditContext;

    public SendMessageCommandHandler(
        IJourneySessionStore sessionStore,
        JourneyConversationService conversationService,
        IAuditContext auditContext)
    {
        _sessionStore = sessionStore;
        _conversationService = conversationService;
        _auditContext = auditContext;
    }

    public async Task<SendMessageOutcome> Handle(SendMessageCommand request, CancellationToken cancellationToken)
    {
        JourneySessionState? session = _sessionStore.Get(request.SessionId);

        if (session is null || session.ParentId != request.ParentId)
        {
            return new SendMessageOutcome(SendMessageStatus.SessionNotFound);
        }

        if (string.IsNullOrWhiteSpace(request.Message))
        {
            return new SendMessageOutcome(SendMessageStatus.MessageRequired);
        }

        SendMessageResponse response = await _conversationService.SendMessageAsync(
            session, request.SessionId, request.Message, cancellationToken);

        _auditContext.MarkLearnerAccess(session.LearnerId, "conversation_sessions");

        return new SendMessageOutcome(SendMessageStatus.Replied, response);
    }
}
