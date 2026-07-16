namespace LearnBridge.Domain.Entities;

public enum GoalStatus
{
    Active,
    Completed,
    Abandoned,
}

/// <summary>
/// Write requires an active <see cref="ParentalConsent"/> for the learner.
/// Offline-relevant — <see cref="UpdatedAt"/> exists for the Phase 5
/// last-write-wins sync.
/// </summary>
public sealed class Goal : ILearnerScoped
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid LearnerId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public GoalStatus Status { get; set; } = GoalStatus.Active;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
