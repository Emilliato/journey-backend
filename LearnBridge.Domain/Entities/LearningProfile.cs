namespace LearnBridge.Domain.Entities;

/// <summary>
/// One per learner. Write requires an active <see cref="ParentalConsent"/>
/// for the learner — enforced in the command/service layer, not here.
/// Offline-relevant (mirrored into IndexedDB per docs/ARCHITECTURE.md) —
/// <see cref="UpdatedAt"/> exists for the Phase 5 last-write-wins sync.
/// </summary>
public sealed class LearningProfile : ILearnerScoped
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid LearnerId { get; set; }

    public string? GradeLevel { get; set; }

    /// <summary>JSON-encoded string[] of subjects the learner is focused on.</summary>
    public string PreferredSubjectsJson { get; set; } = "[]";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
