using LearnBridge.Domain.Features.Dashboard;
using LearnBridge.Domain.Entities;
using Xunit;

namespace LearnBridge.Tests;

public class DashboardCalculatorTests
{
    private static readonly DateTime Now = new(2026, 7, 15, 12, 0, 0, DateTimeKind.Utc);

    private static ConversationSession Session(int daysAgo, int minutes, bool offline = false)
    {
        DateTime start = Now.AddDays(-daysAgo).AddHours(-2);

        return new ConversationSession
        {
            LearnerId = Guid.NewGuid(),
            StartedAt = start,
            EndedAt = start.AddMinutes(minutes),
            WasOffline = offline,
        };
    }

    [Fact]
    public void Streak_CountsConsecutiveSessionDaysBackFromToday()
    {
        var sessions = new List<ConversationSession>
        {
            Session(0, 10),
            Session(1, 15),
            Session(2, 20),
            Session(4, 20), // gap at day 3 breaks the streak
        };

        DashboardStats stats = DashboardCalculator.Compute(Now, sessions, [], []);

        Assert.Equal(3, stats.StreakDays);
    }

    [Fact]
    public void Streak_QuietTodayDoesNotBreakYesterdaysStreak()
    {
        var sessions = new List<ConversationSession> { Session(1, 10), Session(2, 10) };

        DashboardStats stats = DashboardCalculator.Compute(Now, sessions, [], []);

        Assert.Equal(2, stats.StreakDays);
    }

    [Fact]
    public void SevenDaySeries_BucketsOldestFirstAndSumsMinutes()
    {
        var sessions = new List<ConversationSession>
        {
            Session(0, 12),
            Session(0, 8),
            Session(6, 30),
            Session(7, 999), // outside the window
        };

        DashboardStats stats = DashboardCalculator.Compute(Now, sessions, [], []);

        Assert.Equal(7, stats.SessionsPerDay.Count);
        Assert.Equal(1, stats.SessionsPerDay[0]); // 6 days ago
        Assert.Equal(2, stats.SessionsPerDay[6]); // today
        Assert.Equal(30, stats.MinutesPerDay[0]);
        Assert.Equal(20, stats.MinutesPerDay[6]);
        Assert.Equal(3, stats.SessionsLast7Days);
        Assert.Equal(50, stats.LearningMinutesLast7Days);
    }

    [Fact]
    public void GoalCounts_AndOfflineSessions_AreComputed()
    {
        var goals = new List<Goal>
        {
            new() { Status = GoalStatus.Active, UpdatedAt = Now },
            new() { Status = GoalStatus.Active, UpdatedAt = Now },
            new() { Status = GoalStatus.Completed, UpdatedAt = Now.AddDays(-1) },
            new() { Status = GoalStatus.Abandoned, UpdatedAt = Now },
        };
        var sessions = new List<ConversationSession> { Session(0, 10, offline: true), Session(1, 10) };

        DashboardStats stats = DashboardCalculator.Compute(Now, sessions, goals, []);

        Assert.Equal(2, stats.ActiveGoals);
        Assert.Equal(1, stats.CompletedGoals);
        Assert.Equal(1, stats.OfflineSessions);
        Assert.Equal(1, stats.GoalsCompletedPerWeek[^1]);
    }

    [Fact]
    public void Timeline_MergesSessionsGoalsAndSparks_NewestFirst()
    {
        var sessions = new List<ConversationSession> { Session(1, 18, offline: true) };
        var goals = new List<Goal>
        {
            new() { Title = "Space quiz", Status = GoalStatus.Completed, UpdatedAt = Now.AddHours(-1) },
        };
        var memories = new List<JourneyMemory>
        {
            new() { Content = "Brain Spark — Pictures or words? → chose \"Pictures\"", CreatedAt = Now.AddMinutes(-5) },
            new() { Content = "Grasps fractions via pizza examples.", CreatedAt = Now }, // not a spark → excluded
        };

        IReadOnlyList<TimelineEvent> timeline = DashboardCalculator.BuildTimeline(Now, sessions, goals, memories);

        Assert.Equal(3, timeline.Count);
        Assert.Equal("spark", timeline[0].Kind);
        Assert.Equal("goal", timeline[1].Kind);
        Assert.Equal("session", timeline[2].Kind);
        Assert.Contains("18 min · offline", timeline[2].Detail);
        Assert.Contains("Completed: Space quiz", timeline[1].Title);
    }
}
