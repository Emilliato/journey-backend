namespace LearnBridge.Data.Entities;

/// <summary>
/// A school-age learner linked to exactly one parent account. Deliberately
/// minimal — no date of birth, grade, or other identifying detail beyond a
/// display name lives here; that belongs in <see cref="LearningProfile"/>.
/// This is a minor's record, so every field added here should be treated as
/// a deliberate decision, not a default.
/// </summary>
public sealed class Learner
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ParentId { get; set; }

    /// <summary>
    /// The learner's own Identity account (Learner role), created by the
    /// parent together with this profile. Nullable for learners created
    /// before learner accounts existed. See CLAUDE.md constraint 1 (as
    /// amended 2026-07-15): learners sign in with their own credentials.
    /// </summary>
    public Guid? UserId { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
