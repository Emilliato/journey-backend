using LearnBridge.Domain.Features.Sync;
using LearnBridge.Domain.Abstractions;

using LearnBridge.Data;
using LearnBridge.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LearnBridge.Tests;

public class SyncServiceTests
{
    private static LearnBridgeDbContext CreateDbContext()
    {
        DbContextOptions<LearnBridgeDbContext> options = new DbContextOptionsBuilder<LearnBridgeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new LearnBridgeDbContext(options);
    }

    private static (SyncService Service, FakeAuditContext Audit) CreateService(LearnBridgeDbContext dbContext)
    {
        FakeAuditContext audit = new();
        ConsentGate consentGate = new(dbContext);

        return (new SyncService(dbContext, consentGate, audit), audit);
    }

    private static async Task<Learner> SeedLearnerWithActiveConsentAsync(LearnBridgeDbContext dbContext)
    {
        Learner learner = new() { ParentId = Guid.NewGuid(), DisplayName = "Test Learner" };
        dbContext.Learners.Add(learner);
        dbContext.ParentalConsents.Add(new ParentalConsent { LearnerId = learner.Id, ParentId = learner.ParentId });
        await dbContext.SaveChangesAsync();

        return learner;
    }

    [Fact]
    public async Task ApplyBatchAsync_RefusesEverything_WhenConsentNotActive()
    {
        await using LearnBridgeDbContext dbContext = CreateDbContext();
        Learner learner = new() { ParentId = Guid.NewGuid(), DisplayName = "No Consent" };
        dbContext.Learners.Add(learner);
        await dbContext.SaveChangesAsync();

        (SyncService service, _) = CreateService(dbContext);

        SyncBatchRequest request = new(
            learner.Id,
            [new SyncGoalDto(Guid.NewGuid(), "Read more", null, "Active", DateTime.UtcNow)],
            []);

        SyncBatchResult result = await service.ApplyBatchAsync(learner.Id, request, CancellationToken.None);

        Assert.Equal(SyncBatchStatus.ConsentInactive, result.Status);
        Assert.Empty(dbContext.Goals);
    }

    [Fact]
    public async Task ApplyBatchAsync_CreatesANewGoal_WhenNoServerRowExistsYet()
    {
        await using LearnBridgeDbContext dbContext = CreateDbContext();
        Learner learner = await SeedLearnerWithActiveConsentAsync(dbContext);
        (SyncService service, FakeAuditContext audit) = CreateService(dbContext);

        Guid goalId = Guid.NewGuid();
        DateTime updatedAt = DateTime.UtcNow;
        SyncBatchRequest request = new(
            learner.Id,
            [new SyncGoalDto(goalId, "Read every day", "From offline mode", "Active", updatedAt)],
            []);

        SyncBatchResult result = await service.ApplyBatchAsync(learner.Id, request, CancellationToken.None);

        Assert.Equal(SyncBatchStatus.Applied, result.Status);
        SyncGoalDto resolved = Assert.Single(result.Response!.Goals);
        Assert.Equal(goalId, resolved.Id);
        Assert.Equal("Active", resolved.Status);
        Assert.Single(dbContext.Goals);
        Assert.Contains(audit.Marked, a => a.LearnerId == learner.Id && a.Resource == "goals");
    }

    [Fact]
    public async Task ApplyBatchAsync_AppliesTheIncomingGoal_WhenItIsNewerThanTheServerRow()
    {
        await using LearnBridgeDbContext dbContext = CreateDbContext();
        Learner learner = await SeedLearnerWithActiveConsentAsync(dbContext);

        Guid goalId = Guid.NewGuid();
        DateTime serverUpdatedAt = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        dbContext.Goals.Add(new Goal
        {
            Id = goalId,
            LearnerId = learner.Id,
            Title = "Old title",
            Status = GoalStatus.Active,
            UpdatedAt = serverUpdatedAt,
        });
        await dbContext.SaveChangesAsync();

        (SyncService service, _) = CreateService(dbContext);

        DateTime newerUpdatedAt = serverUpdatedAt.AddDays(1);
        SyncBatchRequest request = new(
            learner.Id,
            [new SyncGoalDto(goalId, "Newer title", null, "Completed", newerUpdatedAt)],
            []);

        SyncBatchResult result = await service.ApplyBatchAsync(learner.Id, request, CancellationToken.None);

        SyncGoalDto resolved = Assert.Single(result.Response!.Goals);
        Assert.Equal("Newer title", resolved.Title);
        Assert.Equal("Completed", resolved.Status);

        Goal stored = await dbContext.Goals.SingleAsync();
        Assert.Equal("Newer title", stored.Title);
        Assert.Equal(GoalStatus.Completed, stored.Status);
    }

    [Fact]
    public async Task ApplyBatchAsync_KeepsTheServerGoal_WhenTheIncomingOneIsStale()
    {
        await using LearnBridgeDbContext dbContext = CreateDbContext();
        Learner learner = await SeedLearnerWithActiveConsentAsync(dbContext);

        Guid goalId = Guid.NewGuid();
        DateTime serverUpdatedAt = new(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc);
        dbContext.Goals.Add(new Goal
        {
            Id = goalId,
            LearnerId = learner.Id,
            Title = "Server title",
            Status = GoalStatus.Completed,
            UpdatedAt = serverUpdatedAt,
        });
        await dbContext.SaveChangesAsync();

        (SyncService service, _) = CreateService(dbContext);

        DateTime staleUpdatedAt = serverUpdatedAt.AddDays(-1);
        SyncBatchRequest request = new(
            learner.Id,
            [new SyncGoalDto(goalId, "Stale offline title", null, "Active", staleUpdatedAt)],
            []);

        SyncBatchResult result = await service.ApplyBatchAsync(learner.Id, request, CancellationToken.None);

        // Last-write-wins: the server's newer row wins, and the response
        // reflects that authoritative state so the client overwrites its
        // stale local copy.
        SyncGoalDto resolved = Assert.Single(result.Response!.Goals);
        Assert.Equal("Server title", resolved.Title);
        Assert.Equal("Completed", resolved.Status);

        Goal stored = await dbContext.Goals.SingleAsync();
        Assert.Equal("Server title", stored.Title);
    }

    [Fact]
    public async Task ApplyBatchAsync_RefusesAGoal_WhenTheIdBelongsToAnotherLearner()
    {
        await using LearnBridgeDbContext dbContext = CreateDbContext();
        Learner ownerLearner = await SeedLearnerWithActiveConsentAsync(dbContext);
        Learner attackerLearner = await SeedLearnerWithActiveConsentAsync(dbContext);

        Guid goalId = Guid.NewGuid();
        dbContext.Goals.Add(new Goal
        {
            Id = goalId,
            LearnerId = ownerLearner.Id,
            Title = "Owner's goal",
            Status = GoalStatus.Active,
            UpdatedAt = DateTime.UtcNow,
        });
        await dbContext.SaveChangesAsync();

        (SyncService service, _) = CreateService(dbContext);

        SyncBatchRequest request = new(
            attackerLearner.Id,
            [new SyncGoalDto(goalId, "Hijacked title", null, "Completed", DateTime.UtcNow.AddDays(1))],
            []);

        SyncBatchResult result = await service.ApplyBatchAsync(attackerLearner.Id, request, CancellationToken.None);

        Assert.Empty(result.Response!.Goals);
        Goal stored = await dbContext.Goals.SingleAsync();
        Assert.Equal(ownerLearner.Id, stored.LearnerId);
        Assert.Equal("Owner's goal", stored.Title);
    }

    [Fact]
    public async Task ApplyBatchAsync_CreatesAJourneyMemory_AndIgnoresAnyClientSuppliedSessionId()
    {
        await using LearnBridgeDbContext dbContext = CreateDbContext();
        Learner learner = await SeedLearnerWithActiveConsentAsync(dbContext);
        (SyncService service, FakeAuditContext audit) = CreateService(dbContext);

        Guid memoryId = Guid.NewGuid();
        SyncBatchRequest request = new(
            learner.Id,
            [],
            [new SyncJourneyMemoryDto(
                memoryId,
                Guid.NewGuid(), // a session id that doesn't exist server-side
                "preference",
                "Loves drawing dinosaurs",
                DateTime.UtcNow,
                DateTime.UtcNow)]);

        SyncBatchResult result = await service.ApplyBatchAsync(learner.Id, request, CancellationToken.None);

        SyncJourneyMemoryDto resolved = Assert.Single(result.Response!.JourneyMemories);
        Assert.Null(resolved.ConversationSessionId);
        JourneyMemory stored = await dbContext.JourneyMemories.SingleAsync();
        Assert.Null(stored.ConversationSessionId);
        Assert.Equal(JourneyMemoryCategory.Preference, stored.Category);
        Assert.Contains(audit.Marked, a => a.LearnerId == learner.Id && a.Resource == "journey_memory");
    }

    [Fact]
    public async Task ApplyBatchAsync_RejectsAnUnknownCategory_WithoutWriting()
    {
        await using LearnBridgeDbContext dbContext = CreateDbContext();
        Learner learner = await SeedLearnerWithActiveConsentAsync(dbContext);
        (SyncService service, _) = CreateService(dbContext);

        SyncBatchRequest request = new(
            learner.Id,
            [],
            [new SyncJourneyMemoryDto(
                Guid.NewGuid(), null, "family_relationship", "Should never be a category.", DateTime.UtcNow, DateTime.UtcNow)]);

        SyncBatchResult result = await service.ApplyBatchAsync(learner.Id, request, CancellationToken.None);

        Assert.Empty(result.Response!.JourneyMemories);
        Assert.Empty(dbContext.JourneyMemories);
    }
}
