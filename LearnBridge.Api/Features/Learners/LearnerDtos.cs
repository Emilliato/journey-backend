namespace LearnBridge.Api.Features.Learners;

/// <summary>
/// ConsentGranted must be explicitly true — this is the parent's affirmative
/// consent capture, not a default. See CLAUDE.md constraint 2: no learner
/// row exists without an active parental_consent record from the moment of
/// creation.
/// </summary>
public sealed record CreateLearnerRequest(string DisplayName, bool ConsentGranted);

public sealed record LearnerResponse(Guid Id, string DisplayName, DateTime CreatedAt, bool ConsentActive);
