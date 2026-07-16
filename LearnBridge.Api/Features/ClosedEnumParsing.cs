using LearnBridge.Domain.Entities;

namespace LearnBridge.Api.Features;

/// <summary>
/// Shared string-to-enum parsing for the two closed sets clients submit
/// values for (tool calls in Features/Journey, sync batches in
/// Features/Sync). Kept in one place so both call sites reject the same
/// way — in particular so <see cref="JourneyMemoryCategory"/>'s closed set
/// (CLAUDE.md constraint 4) is enforced identically everywhere a client can
/// write one.
/// </summary>
public static class ClosedEnumParsing
{
    /// <summary>
    /// Case-insensitive because callers disagree on casing by convention,
    /// not by accident: Claude's tool-call inputs use lowercase (see
    /// JourneyTools' schema), while the Angular client's synced records
    /// carry whatever casing its own local models already use.
    /// </summary>
    public static bool TryParseJourneyMemoryCategory(string? raw, out JourneyMemoryCategory category)
    {
        category = default;

        return raw?.ToLowerInvariant() switch
        {
            "academic" => Assign(out category, JourneyMemoryCategory.Academic),
            "preference" => Assign(out category, JourneyMemoryCategory.Preference),
            "engagement" => Assign(out category, JourneyMemoryCategory.Engagement),
            "goal_related" => Assign(out category, JourneyMemoryCategory.GoalRelated),
            _ => false,
        };
    }

    public static bool TryParseGoalStatus(string? raw, out GoalStatus status)
    {
        status = default;

        return raw?.ToLowerInvariant() switch
        {
            "active" => Assign(out status, GoalStatus.Active),
            "completed" => Assign(out status, GoalStatus.Completed),
            "abandoned" => Assign(out status, GoalStatus.Abandoned),
            _ => false,
        };
    }

    private static bool Assign<T>(out T target, T value)
    {
        target = value;
        return true;
    }
}
