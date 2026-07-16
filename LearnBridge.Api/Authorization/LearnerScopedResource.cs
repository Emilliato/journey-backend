using LearnBridge.Domain.Entities;

namespace LearnBridge.Api.Authorization;

/// <summary>
/// A minimal <see cref="ILearnerScoped"/> carrier so a thin endpoint can run
/// the LearnerDataAccess policy against a learner id alone — the
/// <see cref="ParentOwnsLearnerHandler"/> does the ownership lookup — without
/// the presentation layer loading the entity itself.
/// </summary>
public sealed record LearnerScopedResource(Guid LearnerId) : ILearnerScoped;
