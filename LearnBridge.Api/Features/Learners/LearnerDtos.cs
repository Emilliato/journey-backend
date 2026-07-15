namespace LearnBridge.Api.Features.Learners;

/// <summary>
/// ConsentGranted must be explicitly true — this is the parent's affirmative
/// consent capture, not a default. See CLAUDE.md constraint 2: no learner
/// row exists without an active parental_consent record from the moment of
/// creation. Username and Password create the learner's own sign-in
/// (Learner role) — chosen by the parent as part of profile creation.
/// </summary>
public sealed record CreateLearnerRequest(
    string DisplayName,
    bool ConsentGranted,
    string Username,
    string Password);

public sealed record LearnerResponse(
    Guid Id,
    string DisplayName,
    DateTime CreatedAt,
    bool ConsentActive,
    string? AvatarConfig);

/// <summary>Avatar Studio save — the whole config as one JSON string.</summary>
public sealed record UpdateAvatarRequest(string AvatarConfig);

/// <summary>
/// Parent dashboard consent toggle. Active=false revokes (soft delete —
/// RevokedAt is set, the row stays for the audit trail); Active=true grants
/// a fresh consent record.
/// </summary>
public sealed record SetConsentRequest(bool Active);
