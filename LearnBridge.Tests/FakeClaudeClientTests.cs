using LearnBridge.Api.AI.Claude;
using Xunit;

namespace LearnBridge.Tests;

public class FakeClaudeClientTests
{
    private readonly FakeClaudeClient _client = new();

    [Fact]
    public async Task ReturnsUpdateGoalToolUse_WhenMessageMentionsGoal()
    {
        List<ClaudeMessage> history = [ClaudeMessage.UserText("I want to set a goal to read more books")];

        ClaudeTurnResult result = await _client.SendAsync(
            "system", history, JourneyToolsFor.All(), CancellationToken.None);

        Assert.Equal("tool_use", result.StopReason);
        ClaudeContentBlock toolUse = Assert.Single(result.AssistantMessage.Content);
        Assert.Equal("update_goal", toolUse.Name);
    }

    [Fact]
    public async Task ReturnsRecordMemoryToolUse_WhenMessageMentionsAPreference()
    {
        List<ClaudeMessage> history = [ClaudeMessage.UserText("I really love drawing dinosaurs")];

        ClaudeTurnResult result = await _client.SendAsync(
            "system", history, JourneyToolsFor.All(), CancellationToken.None);

        Assert.Equal("tool_use", result.StopReason);
        ClaudeContentBlock toolUse = Assert.Single(result.AssistantMessage.Content);
        Assert.Equal("record_memory", toolUse.Name);
    }

    [Fact]
    public async Task ReturnsPlainTextEndTurn_ForOrdinaryMessages()
    {
        List<ClaudeMessage> history = [ClaudeMessage.UserText("Today was a normal day at school")];

        ClaudeTurnResult result = await _client.SendAsync(
            "system", history, JourneyToolsFor.All(), CancellationToken.None);

        Assert.Equal("end_turn", result.StopReason);
        Assert.Equal(ClaudeContentBlockTypes.Text, Assert.Single(result.AssistantMessage.Content).Type);
    }

    [Fact]
    public async Task ReturnsPlainTextEndTurn_AfterAToolResultIsFedBack()
    {
        List<ClaudeMessage> history =
        [
            ClaudeMessage.UserText("I love drawing dinosaurs"),
            new ClaudeMessage
            {
                Role = ClaudeRoles.Assistant,
                Content = [new ClaudeContentBlock { Type = ClaudeContentBlockTypes.ToolUse, Id = "t1", Name = "record_memory" }],
            },
            new ClaudeMessage
            {
                Role = ClaudeRoles.User,
                Content = [ClaudeContentBlock.ToolResultBlock("t1", "Saved.")],
            },
        ];

        ClaudeTurnResult result = await _client.SendAsync(
            "system", history, JourneyToolsFor.All(), CancellationToken.None);

        Assert.Equal("end_turn", result.StopReason);
    }
}

file static class JourneyToolsFor
{
    // Tests don't need the real schema, just a non-empty tools list — the
    // fake client ignores it entirely, same as it would for a real one.
    public static IReadOnlyList<ClaudeToolDefinition> All() => [];
}
