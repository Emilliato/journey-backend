namespace LearnBridge.Domain.Entities;

/// <summary>
/// Gates every write to <see cref="LearningProfile"/>, <see cref="Goal"/>,
/// and <see cref="JourneyMemory"/> for a given learner — see CLAUDE.md
/// constraint 2. A consent is active while <see cref="RevokedAt"/> is null;
/// revoking is a soft delete (set RevokedAt), never a hard delete, so the
/// consent history itself remains auditable.
/// </summary>
public sealed class ParentalConsent : ILearnerScoped
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid LearnerId { get; set; }

    public Guid ParentId { get; set; }

    public DateTime GrantedAt { get; set; } = DateTime.UtcNow;

    public DateTime? RevokedAt { get; set; }

    public bool IsActive => RevokedAt is null;
}
