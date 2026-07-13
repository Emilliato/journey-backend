using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace LearnBridge.Api.AI.Claude;

/// <summary>
/// Talks to the real Anthropic Messages API. Only constructed when
/// <see cref="ClaudeOptions.ApiKey"/> is configured — see Program.cs and
/// CLAUDE.md constraint 3, the key never leaves this class.
/// </summary>
public sealed class AnthropicClaudeClient : IClaudeClient
{
    private const string AnthropicVersion = "2023-06-01";

    // The Messages API rejects null-valued fields ("tools: Input should be a
    // valid array", extra nulls on content blocks), so nulls must be omitted
    // from the wire JSON entirely.
    private static readonly JsonSerializerOptions WireJsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly HttpClient _httpClient;
    private readonly ClaudeOptions _options;

    public AnthropicClaudeClient(HttpClient httpClient, IOptions<ClaudeOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;

        _httpClient.BaseAddress ??= new Uri("https://api.anthropic.com/");
        _httpClient.DefaultRequestHeaders.Remove("x-api-key");
        _httpClient.DefaultRequestHeaders.Add("x-api-key", _options.ApiKey);
        _httpClient.DefaultRequestHeaders.Remove("anthropic-version");
        _httpClient.DefaultRequestHeaders.Add("anthropic-version", AnthropicVersion);
    }

    public async Task<ClaudeTurnResult> SendAsync(
        string systemPrompt,
        IReadOnlyList<ClaudeMessage> messages,
        IReadOnlyList<ClaudeToolDefinition> tools,
        CancellationToken cancellationToken)
    {
        AnthropicMessageRequest request = new()
        {
            Model = _options.Model,
            MaxTokens = _options.MaxTokens,
            System = systemPrompt,
            Messages = messages.Select(ToWireMessage).ToList(),
            Tools = tools.Count == 0 ? null : tools.Select(ToWireTool).ToList(),
        };

        HttpResponseMessage response = await _httpClient.PostAsJsonAsync("v1/messages", request, WireJsonOptions, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            string errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException(
                $"Anthropic Messages API returned {(int)response.StatusCode}: {errorBody}");
        }

        AnthropicMessageResponse? wireResponse = await response.Content
            .ReadFromJsonAsync<AnthropicMessageResponse>(cancellationToken);

        if (wireResponse is null)
        {
            throw new InvalidOperationException("Anthropic Messages API returned an empty response body.");
        }

        List<ClaudeContentBlock> content = wireResponse.Content.Select(FromWireBlock).ToList();

        return new ClaudeTurnResult
        {
            AssistantMessage = new ClaudeMessage { Role = ClaudeRoles.Assistant, Content = content },
            StopReason = wireResponse.StopReason ?? "end_turn",
        };
    }

    private static AnthropicMessage ToWireMessage(ClaudeMessage message) => new()
    {
        Role = message.Role,
        Content = message.Content.Select(ToWireBlock).ToList(),
    };

    private static AnthropicContentBlock ToWireBlock(ClaudeContentBlock block) => new()
    {
        Type = block.Type,
        Text = block.Type == ClaudeContentBlockTypes.Text ? block.Text : null,
        Id = block.Id,
        Name = block.Name,
        Input = block.Input,
        ToolUseId = block.ToolUseId,
        ToolResultContent = block.Type == ClaudeContentBlockTypes.ToolResult ? block.Content : null,
    };

    private static ClaudeContentBlock FromWireBlock(AnthropicContentBlock block) => new()
    {
        Type = block.Type,
        Text = block.Text,
        Id = block.Id,
        Name = block.Name,
        Input = block.Input,
        ToolUseId = block.ToolUseId,
        Content = block.ToolResultContent,
    };

    private static AnthropicTool ToWireTool(ClaudeToolDefinition tool) => new()
    {
        Name = tool.Name,
        Description = tool.Description,
        InputSchema = tool.InputSchema,
    };
}
