using LearnBridge.Api.AI.Claude;

namespace LearnBridge.Api.Features.Journey;

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

    public async Task<SendMessageResponse> SendMessageAsync(
        JourneySessionState session,
        Guid conversationSessionId,
        string userMessage,
        CancellationToken cancellationToken)
    {
        session.History.Add(ClaudeMessage.UserText(userMessage));

        List<GoalUpdateDto> goalUpdates = [];
        int memoriesRecorded = 0;

        for (int roundTrip = 0; roundTrip < MaxToolRoundTrips; roundTrip++)
        {
            ClaudeTurnResult turn = await _claudeClient.SendAsync(
                JourneyPersona.SystemPrompt, session.History, JourneyTools.All, cancellationToken);

            session.History.Add(turn.AssistantMessage);

            if (turn.StopReason != "tool_use")
            {
                string replyText = string.Join(
                    " ",
                    turn.AssistantMessage.Content
                        .Where(b => b.Type == ClaudeContentBlockTypes.Text)
                        .Select(b => b.Text));

                return new SendMessageResponse(replyText, goalUpdates, memoriesRecorded);
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
            "Sorry, I got a bit tangled up there — could you try rephrasing that?",
            goalUpdates,
            memoriesRecorded);
    }
}
