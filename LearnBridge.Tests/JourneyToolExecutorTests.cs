using System.Text.Json.Nodes;
using LearnBridge.Api.Auditing;
using LearnBridge.Api.Consent;
using LearnBridge.Api.Features.Journey;
using LearnBridge.Data;
using LearnBridge.Data.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LearnBridge.Tests;

public class JourneyToolExecutorTests
{
    private static LearnBridgeDbContext CreateDbContext()
    {
        DbContextOptions<LearnBridgeDbContext> options = new DbContextOptionsBuilder<LearnBridgeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new LearnBridgeDbContext(options);
    }

    private static (JourneyToolExecutor Executor, HttpContext HttpContext) CreateExecutor(LearnBridgeDbContext dbContext)
    {
        DefaultHttpContext httpContext = new();
        FakeHttpContextAccessor accessor = new(httpContext);
        ConsentGate consentGate = new(dbContext);

        return (new JourneyToolExecutor(dbContext, consentGate, accessor), httpContext);
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
    public async Task RecordMemory_WritesRowAndMarksAudit_WhenConsentActive()
    {
        await using LearnBridgeDbContext dbContext = CreateDbContext();
        Learner learner = await SeedLearnerWithActiveConsentAsync(dbContext);
        (JourneyToolExecutor executor, HttpContext httpContext) = CreateExecutor(dbContext);

        JsonObject input = new() { ["category"] = "academic", ["content"] = "Strong at fractions." };

        ToolExecutionResult result = await executor.ExecuteAsync(
            learner.Id, Guid.NewGuid(), JourneyTools.RecordMemory, input, CancellationToken.None);

        Assert.True(result.MemoryRecorded);
        JourneyMemory memory = Assert.Single(dbContext.JourneyMemories);
        Assert.Equal(JourneyMemoryCategory.Academic, memory.Category);
        Assert.Equal(learner.Id, memory.LearnerId);
        Assert.Contains(httpContext.GetMarkedAccesses(), a => a.LearnerId == learner.Id && a.Resource == "journey_memory");
    }

    [Fact]
    public async Task RecordMemory_RefusesToWrite_WhenConsentNotActive()
    {
        await using LearnBridgeDbContext dbContext = CreateDbContext();
        Learner learner = new() { ParentId = Guid.NewGuid(), DisplayName = "No Consent" };
        dbContext.Learners.Add(learner);
        await dbContext.SaveChangesAsync();

        (JourneyToolExecutor executor, _) = CreateExecutor(dbContext);

        JsonObject input = new() { ["category"] = "academic", ["content"] = "Should not be saved." };

        ToolExecutionResult result = await executor.ExecuteAsync(
            learner.Id, Guid.NewGuid(), JourneyTools.RecordMemory, input, CancellationToken.None);

        Assert.False(result.MemoryRecorded);
        Assert.Empty(dbContext.JourneyMemories);
    }

    [Fact]
    public async Task RecordMemory_RejectsAnUnknownCategory_WithoutWriting()
    {
        await using LearnBridgeDbContext dbContext = CreateDbContext();
        Learner learner = await SeedLearnerWithActiveConsentAsync(dbContext);
        (JourneyToolExecutor executor, _) = CreateExecutor(dbContext);

        JsonObject input = new() { ["category"] = "family_relationship", ["content"] = "Should never be a category." };

        ToolExecutionResult result = await executor.ExecuteAsync(
            learner.Id, Guid.NewGuid(), JourneyTools.RecordMemory, input, CancellationToken.None);

        Assert.False(result.MemoryRecorded);
        Assert.Empty(dbContext.JourneyMemories);
    }

    [Fact]
    public async Task UpdateGoal_CreatesNewGoal_WhenNoMatchingTitleExists()
    {
        await using LearnBridgeDbContext dbContext = CreateDbContext();
        Learner learner = await SeedLearnerWithActiveConsentAsync(dbContext);
        (JourneyToolExecutor executor, HttpContext httpContext) = CreateExecutor(dbContext);

        JsonObject input = new() { ["title"] = "Read 10 books", ["status"] = "active" };

        ToolExecutionResult result = await executor.ExecuteAsync(
            learner.Id, Guid.NewGuid(), JourneyTools.UpdateGoal, input, CancellationToken.None);

        Assert.True(result.GoalUpdate!.WasCreated);
        Goal goal = Assert.Single(dbContext.Goals);
        Assert.Equal("Read 10 books", goal.Title);
        Assert.Equal(GoalStatus.Active, goal.Status);
        Assert.Contains(httpContext.GetMarkedAccesses(), a => a.LearnerId == learner.Id && a.Resource == "goals");
    }

    [Fact]
    public async Task UpdateGoal_UpdatesExistingGoal_WhenTitleMatches()
    {
        await using LearnBridgeDbContext dbContext = CreateDbContext();
        Learner learner = await SeedLearnerWithActiveConsentAsync(dbContext);
        dbContext.Goals.Add(new Goal { LearnerId = learner.Id, Title = "Read 10 books", Status = GoalStatus.Active });
        await dbContext.SaveChangesAsync();

        (JourneyToolExecutor executor, _) = CreateExecutor(dbContext);

        JsonObject input = new() { ["title"] = "Read 10 books", ["status"] = "completed" };

        ToolExecutionResult result = await executor.ExecuteAsync(
            learner.Id, Guid.NewGuid(), JourneyTools.UpdateGoal, input, CancellationToken.None);

        Assert.False(result.GoalUpdate!.WasCreated);
        Goal goal = Assert.Single(dbContext.Goals);
        Assert.Equal(GoalStatus.Completed, goal.Status);
    }
}

file sealed class FakeHttpContextAccessor : IHttpContextAccessor
{
    public FakeHttpContextAccessor(HttpContext httpContext)
    {
        HttpContext = httpContext;
    }

    public HttpContext? HttpContext { get; set; }
}
