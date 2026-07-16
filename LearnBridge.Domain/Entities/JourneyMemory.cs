namespace LearnBridge.Domain.Entities;

/// <summary>
/// Closed set, per CLAUDE.md constraint 4 — do not add members without
/// raising it explicitly first. In particular: no health, emotional-state,
/// or family-relationship category, ever. This is a hard boundary given
/// this is a minor's data, not a later refactor.
/// </summary>
public enum JourneyMemoryCategory
{
    Academic,
    Preference,
    Engagement,
    GoalRelated,
}

/// <summary>
/// Write requires an active <see cref="ParentalConsent"/> for the learner.
/// Offline-relevant — <see cref="UpdatedAt"/> exists for the Phase 5
/// last-write-wins sync.
/// </summary>
public sealed class JourneyMemory : ILearnerScoped
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid LearnerId { get; set; }

    public Guid? ConversationSessionId { get; set; }

    public JourneyMemoryCategory Category { get; set; }

    public string Content { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
