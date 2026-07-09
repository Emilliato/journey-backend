namespace LearnBridge.Data.Entities;

/// <summary>
/// One JOURNEY conversation, either routed to Claude (online) or WebLLM
/// (offline) per docs/ARCHITECTURE.md. Offline-relevant — <see cref="UpdatedAt"/>
/// exists for the Phase 5 last-write-wins sync.
/// </summary>
public sealed class ConversationSession : ILearnerScoped
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid LearnerId { get; set; }

    public bool WasOffline { get; set; }

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    public DateTime? EndedAt { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
