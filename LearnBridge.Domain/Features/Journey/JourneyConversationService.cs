using LearnBridge.Domain.Abstractions;

namespace LearnBridge.Domain.Features.Journey;

/// <summary>
/// Runs one user message through JOURNEY: send to Claude, execute any tool
/// calls against the database, feed the results back, and repeat until
/// Claude produces a plain-text reply (or the round-trip cap is hit).
/// </summary>
public sealed class JourneyConversationService
{
    // A real conversational turn should resolve in one or two tool calls at
    // most; this is a backstop against a misbehaving model looping forever,
    // not an expected path.
    private const int MaxToolRoundTrips = 4;

    private readonly IClaudeClient _claudeClient;
    private readonly JourneyToolExecutor _toolExecutor;

    public JourneyConversationService(IClaudeClient claudeClient, JourneyToolExecutor toolExecutor)
    {
        _claudeClient = claudeClient;
        _toolExecutor = toolExecutor;
    }

    public Task<SendMessageResponse> SendMessageAsync(
        JourneySessionState session,
        Guid conversationSessionId,
        string userMessage,
        CancellationToken cancellationToken)
    {
        session.History.Add(ClaudeMessage.UserText(userMessage));
        return RunTurnLoopAsync(session, conversationSessionId, cancellationToken);
    }

    /// <summary>
    /// Opens a fresh session with JOURNEY speaking first — a hidden
    /// bootstrap user turn (the Messages API requires the first message to
    /// be a user turn) prompts the greeting/introduction described in the
    /// system prompt. The frontend never renders the bootstrap message.
    /// </summary>
    public Task<SendMessageResponse> StartConversationAsync(
        JourneySessionState session,
        Guid conversationSessionId,
        CancellationToken cancellationToken)
    {
        session.History.Add(ClaudeMessage.UserText(JourneyPersona.SessionStartMessage));
        return RunTurnLoopAsync(session, conversationSessionId, cancellationToken);
    }

    private async Task<SendMessageResponse> RunTurnLoopAsync(
        JourneySessionState session,
        Guid conversationSessionId,
        CancellationToken cancellationToken)
    {
        List<GoalUpdateDto> goalUpdates = [];
        int memoriesRecorded = 0;

        // The model often puts its user-facing text in the same turn as a
        // tool call ("Nice, grade 6! *records memory*") and has little or
        // nothing to add once the tool result comes back — so the reply is
        // the text of EVERY assistant turn in the loop, not just the last.
        List<string> replyParts = [];

        for (int roundTrip = 0; roundTrip < MaxToolRoundTrips; roundTrip++)
        {
            ClaudeTurnResult turn = await _claudeClient.SendAsync(
                session.SystemPrompt, session.History, JourneyTools.All, cancellationToken);

            session.History.Add(turn.AssistantMessage);

            replyParts.AddRange(turn.AssistantMessage.Content
                .Where(b => b.Type == ClaudeContentBlockTypes.Text && !string.IsNullOrWhiteSpace(b.Text))
                .Select(b => b.Text!));

            if (turn.StopReason != "tool_use")
            {
                return new SendMessageResponse(string.Join("\n\n", replyParts), goalUpdates, memoriesRecorded);
            }

            List<ClaudeContentBlock> toolResults = [];

            foreach (ClaudeContentBlock toolUse in turn.AssistantMessage.Content
                .Where(b => b.Type == ClaudeContentBlockTypes.ToolUse))
            {
                ToolExecutionResult result = await _toolExecutor.ExecuteAsync(
                    session.LearnerId, conversationSessionId, toolUse.Name!, toolUse.Input, cancellationToken);

                if (result.GoalUpdate is not null)
                {
                    goalUpdates.Add(result.GoalUpdate);
                }

                if (result.MemoryRecorded)
                {
                    memoriesRecorded++;
                }

                toolResults.Add(ClaudeContentBlock.ToolResultBlock(toolUse.Id!, result.ResultText));
            }

            session.History.Add(new ClaudeMessage { Role = ClaudeRoles.User, Content = toolResults });
        }

        return new SendMessageResponse(
            replyParts.Count > 0
                ? string.Join("\n\n", replyParts)
                : "Sorry, I got a bit tangled up there — could you try rephrasing that?",
            goalUpdates,
            memoriesRecorded);
    }
}
