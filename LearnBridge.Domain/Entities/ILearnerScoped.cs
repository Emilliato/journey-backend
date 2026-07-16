namespace LearnBridge.Domain.Entities;

/// <summary>
/// Implemented by any entity whose row belongs to exactly one learner, so
/// authorization handlers can check row ownership generically instead of
/// writing a separate handler per entity type. <see cref="Learner"/> itself
/// does not implement this — it *is* the learner, not a learner-scoped
/// child row, and is handled separately in the authorization layer.
/// </summary>
public interface ILearnerScoped
{
    Guid LearnerId { get; }
}
