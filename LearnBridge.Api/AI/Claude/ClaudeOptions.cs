namespace LearnBridge.Api.AI.Claude;

public sealed class ClaudeOptions
{
    /// <summary>
    /// Empty by default. Local dev: `dotnet user-secrets set Claude:ApiKey "..."`.
    /// See CLAUDE.md constraint 3 — this key must never leave the backend.
    /// While empty, <see cref="FakeClaudeClient"/> is used instead of the
    /// real Anthropic client, so the rest of the proxy is still exercisable
    /// end-to-end without a live key.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// No default guessed here deliberately — set this to a real Anthropic
    /// Messages API model id once ApiKey is configured.
    /// </summary>
    public string Model { get; set; } = string.Empty;

    public int MaxTokens { get; set; } = 1024;
}
