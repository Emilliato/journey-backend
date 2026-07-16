using LearnBridge.Domain.Abstractions;
using System.Text.Json.Nodes;
using LearnBridge.Domain.Features.Journey;

namespace LearnBridge.Api.AI.Claude;

/// <summary>
/// Used instead of <see cref="AnthropicClaudeClient"/> whenever
/// <see cref="ClaudeOptions.ApiKey"/> is unset — see Program.cs. Lets the
/// rest of the proxy (session lifecycle, tool execution, consent gating,
/// audit logging, the Angular chat UI) be built and exercised end-to-end
/// without a live Anthropic key, mirroring the EchoMate backend's
/// FakeTextToSpeechService pattern.
///
/// Deterministic, not an LLM: it looks at the newest user turn's text and
/// picks a tool call by simple keyword match, purely so the tool-use round
/// trip is genuinely observable in local dev.
/// </summary>
public sealed class FakeClaudeClient : IClaudeClient
{
    private static readonly string[] MemoryKeywords = ["like", "love", "enjoy", "favorite", "favourite"];

    public Task<ClaudeTurnResult> SendAsync(
        string systemPrompt,
        IReadOnlyList<ClaudeMessage> messages,
        IReadOnlyList<ClaudeToolDefinition> tools,
        CancellationToken cancellationToken)
    {
        ClaudeMessage? lastMessage = messages.LastOrDefault();

        bool lastMessageIsToolResult = lastMessage?.Role == ClaudeRoles.User &&
            lastMessage.Content.Any(b => b.Type == ClaudeContentBlockTypes.ToolResult);

        if (lastMessageIsToolResult)
        {
            return Task.FromResult(EndTurn("Got it — I've noted that down. Anything else on your mind?"));
        }

        string latestUserText = lastMessage?.Content
            .Where(b => b.Type == ClaudeContentBlockTypes.Text)
            .Select(b => b.Text ?? string.Empty)
            .LastOrDefault() ?? string.Empty;

        if (latestUserText.StartsWith(JourneyPersona.SessionStartMarker, StringComparison.Ordinal))
        {
            return Task.FromResult(EndTurn(
                "Hi, I'm JOURNEY! Before we get started, I'd love to get to know you a little. " +
                "What grade are you in this year?"));
        }

        if (latestUserText.Contains("goal", StringComparison.OrdinalIgnoreCase))
        {
            JsonObject input = new()
            {
                ["title"] = ExtractGoalTitle(latestUserText),
                ["status"] = "active",
            };

            return Task.FromResult(ToolUse("update_goal", input));
        }

        if (MemoryKeywords.Any(keyword => latestUserText.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
        {
            JsonObject input = new()
            {
                ["category"] = "preference",
                ["content"] = Truncate(latestUserText, 500),
            };

            return Task.FromResult(ToolUse("record_memory", input));
        }

        return Task.FromResult(EndTurn(
            "That's great to hear! Tell me more, or let me know if you'd like to set a learning goal."));
    }

    private static ClaudeTurnResult EndTurn(string text) => new()
    {
        AssistantMessage = new ClaudeMessage
        {
            Role = ClaudeRoles.Assistant,
            Content = [ClaudeContentBlock.TextBlock(text)],
        },
        StopReason = "end_turn",
    };

    private static ClaudeTurnResult ToolUse(string name, JsonObject input) => new()
    {
        AssistantMessage = new ClaudeMessage
        {
            Role = ClaudeRoles.Assistant,
            Content =
            [
                new ClaudeContentBlock
                {
                    Type = ClaudeContentBlockTypes.ToolUse,
                    Id = $"fake_tool_{Guid.NewGuid():N}",
                    Name = name,
                    Input = input,
                },
            ],
        },
        StopReason = "tool_use",
    };

    private static string ExtractGoalTitle(string text)
    {
        string trimmed = Truncate(text.Trim(), 80);
        return string.IsNullOrWhiteSpace(trimmed) ? "New learning goal" : trimmed;
    }

    private static string Truncate(string text, int maxLength) =>
        text.Length <= maxLength ? text : text[..maxLength];
}
