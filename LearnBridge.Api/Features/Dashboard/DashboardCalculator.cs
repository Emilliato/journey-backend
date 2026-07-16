using LearnBridge.Domain.Entities;

namespace LearnBridge.Api.Features.Dashboard;

/// <summary>
/// Pure aggregation over a learner's sessions, goals, and memories — split
/// out from the endpoint so the streak/series maths is unit-testable
/// without a database. All day bucketing is UTC-date based, matching how
/// the entities store timestamps.
/// </summary>
public static class DashboardCalculator
{
    public static DashboardStats Compute(
        DateTime utcNow,
        IReadOnlyList<ConversationSession> sessions,
        IReadOnlyList<Goal> goals,
        IReadOnlyList<JourneyMemory> memories)
    {
        DateOnly today = DateOnly.FromDateTime(utcNow);

        HashSet<DateOnly> sessionDays = sessions
            .Select(s => DateOnly.FromDateTime(s.StartedAt))
            .ToHashSet();

        // Streak: consecutive days with at least one session, counting back
        // from today — a quiet "today so far" doesn't break it, the count
        // just starts from yesterday.
        int streak = 0;
        DateOnly cursor = sessionDays.Contains(today) ? today : today.AddDays(-1);

        while (sessionDays.Contains(cursor))
        {
            streak++;
            cursor = cursor.AddDays(-1);
        }

        int[] sessionsPerDay = new int[7];
        int[] minutesPerDay = new int[7];

        foreach (ConversationSession session in sessions)
        {
            DateOnly day = DateOnly.FromDateTime(session.StartedAt);
            int offset = today.DayNumber - day.DayNumber; // 0 = today

            if (offset is < 0 or > 6)
            {
                continue;
            }

            int index = 6 - offset; // oldest first
            sessionsPerDay[index]++;
            minutesPerDay[index] += SessionMinutes(session, utcNow);
        }

        int[] goalsCompletedPerWeek = new int[12];

        foreach (Goal goal in goals.Where(g => g.Status == GoalStatus.Completed))
        {
            int daysAgo = today.DayNumber - DateOnly.FromDateTime(goal.UpdatedAt).DayNumber;
            int weeksAgo = daysAgo / 7;

            if (weeksAgo is >= 0 and < 12)
            {
                goalsCompletedPerWeek[11 - weeksAgo]++;
            }
        }

        List<CategoryCount> categoryCounts = memories
            .GroupBy(m => m.Category)
            .Select(g => new CategoryCount(ToCategoryString(g.Key), g.Count()))
            .OrderByDescending(c => c.Count)
            .ToList();

        return new DashboardStats(
            StreakDays: streak,
            SessionsLast7Days: sessionsPerDay.Sum(),
            LearningMinutesLast7Days: minutesPerDay.Sum(),
            ActiveGoals: goals.Count(g => g.Status == GoalStatus.Active),
            CompletedGoals: goals.Count(g => g.Status == GoalStatus.Completed),
            SessionsPerDay: sessionsPerDay,
            MinutesPerDay: minutesPerDay,
            GoalsCompletedPerWeek: goalsCompletedPerWeek,
            MemoryCategoryCounts: categoryCounts,
            OfflineSessions: sessions.Count(s => s.WasOffline));
    }

    public static IReadOnlyList<TimelineEvent> BuildTimeline(
        DateTime utcNow,
        IReadOnlyList<ConversationSession> sessions,
        IReadOnlyList<Goal> goals,
        IReadOnlyList<JourneyMemory> memories,
        int limit = 25)
    {
        List<TimelineEvent> events = [];

        foreach (ConversationSession session in sessions)
        {
            int minutes = SessionMinutes(session, utcNow);
            string mode = session.WasOffline ? "offline" : "online";
            events.Add(new TimelineEvent(
                "session",
                "Chatted with JOURNEY",
                minutes > 0 ? $"{minutes} min · {mode}" : mode,
                session.StartedAt));
        }

        foreach (Goal goal in goals)
        {
            string title = goal.Status switch
            {
                GoalStatus.Completed => $"Completed: {goal.Title} 🎉",
                GoalStatus.Abandoned => $"Paused: {goal.Title}",
                _ => $"Working on: {goal.Title}",
            };

            events.Add(new TimelineEvent("goal", title, null, goal.UpdatedAt));
        }

        // Brain Spark answers are memories with the shared content prefix —
        // see BrainSparkQuestionBank.MemoryContent.
        foreach (JourneyMemory memory in memories.Where(m => m.Content.StartsWith("Brain Spark — ", StringComparison.Ordinal)))
        {
            events.Add(new TimelineEvent(
                "spark",
                "Brain Spark answered",
                memory.Content["Brain Spark — ".Length..],
                memory.CreatedAt));
        }

        return events
            .OrderByDescending(e => e.At)
            .Take(limit)
            .ToList();
    }

    private static int SessionMinutes(ConversationSession session, DateTime utcNow)
    {
        DateTime end = session.EndedAt ?? (session.StartedAt.Date == utcNow.Date ? utcNow : session.StartedAt);

        return Math.Max(0, (int)Math.Round((end - session.StartedAt).TotalMinutes));
    }

    private static string ToCategoryString(JourneyMemoryCategory category) => category switch
    {
        JourneyMemoryCategory.Academic => "academic",
        JourneyMemoryCategory.Preference => "preference",
        JourneyMemoryCategory.Engagement => "engagement",
        JourneyMemoryCategory.GoalRelated => "goal_related",
        _ => throw new InvalidOperationException($"Unhandled category '{category}'."),
    };
}
