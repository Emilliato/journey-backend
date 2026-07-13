using System.Collections.Concurrent;
using LearnBridge.Api.AI.Claude;

namespace LearnBridge.Api.Features.Journey;

public sealed class JourneySessionState
{
    public required Guid LearnerId { get; init; }

    public required Guid ParentId { get; init; }

    /// <summary>
    /// Built once at session start from the learner's journey_memory rows —
    /// see <see cref="JourneyPersona.BuildSystemPrompt"/>. Memories recorded
    /// mid-session don't need to be re-injected; they're already in History.
    /// </summary>
    public required string SystemPrompt { get; init; }

    public List<ClaudeMessage> History { get; } = [];
}

/// <summary>
/// Holds the live conversation transcript for the lifetime of a session
/// only — per docs/ARCHITECTURE.md, the durable record of a conversation is
/// what it produces (journey_memory rows, goal updates), not the raw
/// transcript, so this is intentionally in-memory and lost on restart or
/// on Complete(), same shape as the EchoMate backend's session store.
/// </summary>
public interface IJourneySessionStore
{
    JourneySessionState Create(Guid sessionId, Guid learnerId, Guid parentId, string systemPrompt);

    JourneySessionState? Get(Guid sessionId);

    void Complete(Guid sessionId);
}

public sealed class InMemoryJourneySessionStore : IJourneySessionStore
{
    private readonly ConcurrentDictionary<Guid, JourneySessionState> _sessions = new();

    public JourneySessionState Create(Guid sessionId, Guid learnerId, Guid parentId, string systemPrompt)
    {
        JourneySessionState state = new() { LearnerId = learnerId, ParentId = parentId, SystemPrompt = systemPrompt };
        _sessions[sessionId] = state;
        return state;
    }

    public JourneySessionState? Get(Guid sessionId)
    {
        return _sessions.TryGetValue(sessionId, out JourneySessionState? state) ? state : null;
    }

    public void Complete(Guid sessionId)
    {
        _sessions.TryRemove(sessionId, out _);
    }
}
