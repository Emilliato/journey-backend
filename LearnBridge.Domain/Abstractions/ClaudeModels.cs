using System.Text.Json.Nodes;

namespace LearnBridge.Domain.Abstractions;

public static class ClaudeRoles
{
    public const string User = "user";
    public const string Assistant = "assistant";
}

public static class ClaudeContentBlockTypes
{
    public const string Text = "text";
    public const string ToolUse = "tool_use";
    public const string ToolResult = "tool_result";
}

/// <summary>
/// One content block within a Claude message. Only the fields relevant to
/// its <see cref="Type"/> are populated — this mirrors the Anthropic
/// Messages API's tagged-union content blocks without a full polymorphic
/// serialization setup, which isn't worth it for three block shapes.
/// </summary>
public sealed class ClaudeContentBlock
{
    public required string Type { get; init; }

    // Text blocks.
    public string? Text { get; init; }

    // Tool-use blocks (assistant asking us to run a tool).
    public string? Id { get; init; }
    public string? Name { get; init; }
    public JsonObject? Input { get; init; }

    // Tool-result blocks (our reply to a tool-use block).
    public string? ToolUseId { get; init; }
    public string? Content { get; init; }

    public static ClaudeContentBlock TextBlock(string text) =>
        new() { Type = ClaudeContentBlockTypes.Text, Text = text };

    public static ClaudeContentBlock ToolResultBlock(string toolUseId, string content) =>
        new() { Type = ClaudeContentBlockTypes.ToolResult, ToolUseId = toolUseId, Content = content };
}

public sealed class ClaudeMessage
{
    public required string Role { get; init; }

    public required List<ClaudeContentBlock> Content { get; init; }

    public static ClaudeMessage UserText(string text) =>
        new() { Role = ClaudeRoles.User, Content = [ClaudeContentBlock.TextBlock(text)] };
}

public sealed class ClaudeToolDefinition
{
    public required string Name { get; init; }

    public required string Description { get; init; }

    public required JsonObject InputSchema { get; init; }
}

/// <summary>
/// The assistant's reply for one round trip, plus whether Claude wants to
/// call a tool (stop_reason == "tool_use") or is done (anything else).
/// </summary>
public sealed class ClaudeTurnResult
{
    public required ClaudeMessage AssistantMessage { get; init; }

    public required string StopReason { get; init; }
}
