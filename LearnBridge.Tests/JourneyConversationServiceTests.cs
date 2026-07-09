using LearnBridge.Api.AI.Claude;
using LearnBridge.Api.Consent;
using LearnBridge.Api.Features.Journey;
using LearnBridge.Data;
using LearnBridge.Data.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LearnBridge.Tests;

public class JourneyConversationServiceTests
{
    private static LearnBridgeDbContext CreateDbContext()
    {
        DbContextOptions<LearnBridgeDbContext> options = new DbContextOptionsBuilder<LearnBridgeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new LearnBridgeDbContext(options);
    }

    [Fact]
    public async Task SendMessageAsync_ExecutesToolCall_AndReturnsFinalTextReply()
    {
        await using LearnBridgeDbContext dbContext = CreateDbContext();

        Learner learner = new() { ParentId = Guid.NewGuid(), DisplayName = "Test Learner" };
        dbContext.Learners.Add(learner);
        dbContext.ParentalConsents.Add(new ParentalConsent { LearnerId = learner.Id, ParentId = learner.ParentId });
        await dbContext.SaveChangesAsync();

        IHttpContextAccessor accessor = new FakeAccessor(new DefaultHttpContext());
        JourneyToolExecutor toolExecutor = new(dbContext, new ConsentGate(dbContext), accessor);
        JourneyConversationService service = new(new FakeClaudeClient(), toolExecutor);

        JourneySessionState session = new() { LearnerId = learner.Id, ParentId = learner.ParentId };

        SendMessageResponse response = await service.SendMessageAsync(
            session, Guid.NewGuid(), "I want to set a goal to finish my science project", CancellationToken.None);

        GoalUpdateDto update = Assert.Single(response.GoalUpdates);
        Assert.True(update.WasCreated);
        Assert.False(string.IsNullOrWhiteSpace(response.Reply));
        Assert.Single(dbContext.Goals);

        // History should hold: user text, assistant tool_use, user tool_result, assistant text.
        Assert.Equal(4, session.History.Count);
    }

    [Fact]
    public async Task SendMessageAsync_ReturnsPlainReply_WithNoToolCalls_ForOrdinaryMessages()
    {
        await using LearnBridgeDbContext dbContext = CreateDbContext();

        Learner learner = new() { ParentId = Guid.NewGuid(), DisplayName = "Test Learner" };
        dbContext.Learners.Add(learner);
        dbContext.ParentalConsents.Add(new ParentalConsent { LearnerId = learner.Id, ParentId = learner.ParentId });
        await dbContext.SaveChangesAsync();

        IHttpContextAccessor accessor = new FakeAccessor(new DefaultHttpContext());
        JourneyToolExecutor toolExecutor = new(dbContext, new ConsentGate(dbContext), accessor);
        JourneyConversationService service = new(new FakeClaudeClient(), toolExecutor);

        JourneySessionState session = new() { LearnerId = learner.Id, ParentId = learner.ParentId };

        SendMessageResponse response = await service.SendMessageAsync(
            session, Guid.NewGuid(), "Just saying hello", CancellationToken.None);

        Assert.Empty(response.GoalUpdates);
        Assert.Equal(0, response.MemoriesRecorded);
        Assert.False(string.IsNullOrWhiteSpace(response.Reply));
        Assert.Empty(dbContext.Goals);
        Assert.Empty(dbContext.JourneyMemories);
    }
}

file sealed class FakeAccessor : IHttpContextAccessor
{
    public FakeAccessor(HttpContext httpContext)
    {
        HttpContext = httpContext;
    }

    public HttpContext? HttpContext { get; set; }
}
