using LearnBridge.Domain.Abstractions;
using LearnBridge.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LearnBridge.Domain.Features.Journey;

public enum StartSessionStatus
{
    LearnerNotFound,
    ConsentInactive,
    Started,
}

public sealed record StartSessionResult(StartSessionStatus Status, StartSessionResponse? Response = null);

/// <summary>
/// Opens a JOURNEY session: persists a ConversationSession, builds the
/// per-learner system prompt from journey_memory, seeds the in-memory session
/// transcript, and lets JOURNEY speak first. Consent-gated (constraint 2) and
/// audited (constraint 5). Authorization happens in the endpoint.
/// </summary>
public sealed record StartSessionCommand(Guid LearnerId, Guid ParentId) : IRequest<StartSessionResult>;

internal sealed class StartSessionCommandHandler : IRequestHandler<StartSessionCommand, StartSessionResult>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ConsentGate _consentGate;
    private readonly IJourneySessionStore _sessionStore;
    private readonly JourneyConversationService _conversationService;
    private readonly IAuditContext _auditContext;

    public StartSessionCommandHandler(
        IApplicationDbContext dbContext,
        ConsentGate consentGate,
        IJourneySessionStore sessionStore,
        JourneyConversationService conversationService,
        IAuditContext auditContext)
    {
        _dbContext = dbContext;
        _consentGate = consentGate;
        _sessionStore = sessionStore;
        _conversationService = conversationService;
        _auditContext = auditContext;
    }

    public async Task<StartSessionResult> Handle(StartSessionCommand request, CancellationToken cancellationToken)
    {
        Learner? learner = await _dbContext.Learners
            .FirstOrDefaultAsync(l => l.Id == request.LearnerId, cancellationToken);

        if (learner is null)
        {
            return new StartSessionResult(StartSessionStatus.LearnerNotFound);
        }

        if (!await _consentGate.IsActiveAsync(learner.Id, cancellationToken))
        {
            return new StartSessionResult(StartSessionStatus.ConsentInactive);
        }

        ConversationSession session = new() { LearnerId = learner.Id, WasOffline = false };
        _dbContext.ConversationSessions.Add(session);
        await _dbContext.SaveChangesAsync(cancellationToken);

        // The learner's memory repository, replayed into the system prompt so
        // JOURNEY knows them (and runs the introduction when there's nothing
        // yet). Newest-first cap, then chronological for the prompt.
        List<JourneyMemory> memories = await _dbContext.JourneyMemories
            .Where(m => m.LearnerId == learner.Id)
            .OrderByDescending(m => m.CreatedAt)
            .Take(JourneyPersona.MaxMemoriesInPrompt)
            .ToListAsync(cancellationToken);
        memories.Reverse();

        string systemPrompt = JourneyPersona.BuildSystemPrompt(learner.DisplayName, memories);
        JourneySessionState state = _sessionStore.Create(session.Id, learner.Id, request.ParentId, systemPrompt);

        SendMessageResponse opening = await _conversationService.StartConversationAsync(
            state, session.Id, cancellationToken);

        _auditContext.MarkLearnerAccess(learner.Id, "conversation_sessions");
        _auditContext.MarkLearnerAccess(learner.Id, "journey_memory");

        return new StartSessionResult(
            StartSessionStatus.Started,
            new StartSessionResponse(session.Id, session.StartedAt, opening.Reply));
    }
}
