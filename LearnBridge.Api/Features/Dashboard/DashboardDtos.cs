namespace LearnBridge.Api.Features.Dashboard;

public sealed record DashboardStats(
    int StreakDays,
    int SessionsLast7Days,
    int LearningMinutesLast7Days,
    int ActiveGoals,
    int CompletedGoals,
    IReadOnlyList<int> SessionsPerDay,
    IReadOnlyList<int> MinutesPerDay,
    IReadOnlyList<int> GoalsCompletedPerWeek,
    IReadOnlyList<CategoryCount> MemoryCategoryCounts,
    int OfflineSessions);

public sealed record CategoryCount(string Category, int Count);

public sealed record TimelineEvent(
    string Kind, // session | goal | spark
    string Title,
    string? Detail,
    DateTime At);

public sealed record DashboardResponse(
    DashboardStats Stats,
    IReadOnlyList<TimelineEvent> Timeline);
