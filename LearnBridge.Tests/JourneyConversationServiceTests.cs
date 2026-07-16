using LearnBridge.Api.AI.Claude;
using LearnBridge.Api.Consent;
using LearnBridge.Api.Features.Journey;
using LearnBridge.Data;
using LearnBridge.Domain.Entities;
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

        JourneySessionState session = new()
        {
            LearnerId = learner.Id,
            ParentId = learner.ParentId,
            SystemPrompt = JourneyPersona.BuildSystemPrompt(learner.DisplayName, []),
        };

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

        JourneySessionState session = new()
        {
            LearnerId = learner.Id,
            ParentId = learner.ParentId,
            SystemPrompt = JourneyPersona.BuildSystemPrompt(learner.DisplayName, []),
        };

        SendMessageResponse response = await service.SendMessageAsync(
            session, Guid.NewGuid(), "Just saying hello", CancellationToken.None);

        Assert.Empty(response.GoalUpdates);
        Assert.Equal(0, response.MemoriesRecorded);
        Assert.False(string.IsNullOrWhiteSpace(response.Reply));
        Assert.Empty(dbContext.Goals);
        Assert.Empty(dbContext.JourneyMemories);
    }

    [Fact]
    public async Task StartConversationAsync_ProducesAnOpeningReply_FromAHiddenBootstrapTurn()
    {
        await using LearnBridgeDbContext dbContext = CreateDbContext();

        Learner learner = new() { ParentId = Guid.NewGuid(), DisplayName = "Test Learner" };
        dbContext.Learners.Add(learner);
        dbContext.ParentalConsents.Add(new ParentalConsent { LearnerId = learner.Id, ParentId = learner.ParentId });
        await dbContext.SaveChangesAsync();

        IHttpContextAccessor accessor = new FakeAccessor(new DefaultHttpContext());
        JourneyToolExecutor toolExecutor = new(dbContext, new ConsentGate(dbContext), accessor);
        JourneyConversationService service = new(new FakeClaudeClient(), toolExecutor);

        JourneySessionState session = new()
        {
            LearnerId = learner.Id,
            ParentId = learner.ParentId,
            SystemPrompt = JourneyPersona.BuildSystemPrompt(learner.DisplayName, []),
        };

        SendMessageResponse response = await service.StartConversationAsync(
            session, Guid.NewGuid(), CancellationToken.None);

        Assert.False(string.IsNullOrWhiteSpace(response.Reply));
        // The greeting itself must not leak the bootstrap marker to the UI.
        Assert.DoesNotContain(JourneyPersona.SessionStartMarker, response.Reply);
        // History: hidden bootstrap user turn + assistant greeting.
        Assert.Equal(2, session.History.Count);
        Assert.StartsWith(JourneyPersona.SessionStartMarker, session.History[0].Content[0].Text);
    }
}

public class JourneyPersonaTests
{
    [Fact]
    public void BuildSystemPrompt_WithNoMemories_RunsTheIntroductionFlow()
    {
        string prompt = JourneyPersona.BuildSystemPrompt("Emma", []);

        Assert.Contains("Getting to know Emma", prompt);
        Assert.Contains("ONE AT A TIME", prompt);
        Assert.Contains("record_memory", prompt);
    }

    [Fact]
    public void BuildSystemPrompt_WithMemories_InjectsThemAndSkipsTheIntroduction()
    {
        List<JourneyMemory> memories =
        [
            new() { Category = JourneyMemoryCategory.Academic, Content = "Is in grade 6." },
            new() { Category = JourneyMemoryCategory.GoalRelated, Content = "Wants to master fractions." },
        ];

        string prompt = JourneyPersona.BuildSystemPrompt("Emma", memories);

        Assert.Contains("What you already know about Emma", prompt);
        Assert.Contains("(academic) Is in grade 6.", prompt);
        Assert.Contains("(goal-related) Wants to master fractions.", prompt);
        Assert.DoesNotContain("Getting to know Emma", prompt);
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
