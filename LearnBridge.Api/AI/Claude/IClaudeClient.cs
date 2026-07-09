namespace LearnBridge.Api.AI.Claude;

public interface IClaudeClient
{
    Task<ClaudeTurnResult> SendAsync(
        string systemPrompt,
        IReadOnlyList<ClaudeMessage> messages,
        IReadOnlyList<ClaudeToolDefinition> tools,
        CancellationToken cancellationToken);
}
