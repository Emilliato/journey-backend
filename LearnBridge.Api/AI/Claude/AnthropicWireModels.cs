using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace LearnBridge.Api.AI.Claude;

/// <summary>
/// Wire-format DTOs for the Anthropic Messages API (snake_case JSON),
/// kept separate from the internal <see cref="ClaudeMessage"/> model so
/// changes to Anthropic's schema don't ripple into the rest of the proxy.
/// </summary>
public sealed class AnthropicMessageRequest
{
    [JsonPropertyName("model")]
    public required string Model { get; init; }

    [JsonPropertyName("max_tokens")]
    public required int MaxTokens { get; init; }

    [JsonPropertyName("system")]
    public required string System { get; init; }

    [JsonPropertyName("messages")]
    public required List<AnthropicMessage> Messages { get; init; }

    [JsonPropertyName("tools")]
    public List<AnthropicTool>? Tools { get; init; }
}

public sealed class AnthropicMessage
{
    [JsonPropertyName("role")]
    public required string Role { get; init; }

    [JsonPropertyName("content")]
    public required List<AnthropicContentBlock> Content { get; init; }
}

public sealed class AnthropicContentBlock
{
    [JsonPropertyName("type")]
    public required string Type { get; init; }

    [JsonPropertyName("text")]
    public string? Text { get; init; }

    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("input")]
    public JsonObject? Input { get; init; }

    [JsonPropertyName("tool_use_id")]
    public string? ToolUseId { get; init; }

    /// <summary>
    /// Only present on tool_result blocks — a different JSON property
    /// ("content") than a text block's "text", even though both end up
    /// holding a string in this DTO.
    /// </summary>
    [JsonPropertyName("content")]
    public string? ToolResultContent { get; init; }
}

public sealed class AnthropicTool
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("description")]
    public required string Description { get; init; }

    [JsonPropertyName("input_schema")]
    public required JsonObject InputSchema { get; init; }
}

public sealed class AnthropicMessageResponse
{
    [JsonPropertyName("content")]
    public List<AnthropicContentBlock> Content { get; init; } = [];

    [JsonPropertyName("stop_reason")]
    public string? StopReason { get; init; }
}
