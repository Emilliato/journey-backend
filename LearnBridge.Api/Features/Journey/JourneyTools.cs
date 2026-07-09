using System.Text.Json.Nodes;
using LearnBridge.Api.AI.Claude;
using LearnBridge.Data.Entities;

namespace LearnBridge.Api.Features.Journey;

/// <summary>
/// The two tools JOURNEY can call during a conversation. The category enum
/// here is intentionally the same four values as
/// <see cref="JourneyMemoryCategory"/> and nothing else — see CLAUDE.md
/// constraint 4. Claude is schema-constrained to these four strings, and
/// the handler in JourneyConversationService parses straight into the C#
/// enum, so there is no path for a fifth category to reach the database.
/// </summary>
public static class JourneyTools
{
    public const string RecordMemory = "record_memory";
    public const string UpdateGoal = "update_goal";

    public static IReadOnlyList<ClaudeToolDefinition> All { get; } =
    [
        new ClaudeToolDefinition
        {
            Name = RecordMemory,
            Description =
                "Record a durable, worth-remembering fact about the learner. " +
                "Only for academic strengths/gaps, preferences, engagement " +
                "patterns, or goal-related notes — never health, emotional " +
                "state, or family information.",
            InputSchema = new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    ["category"] = new JsonObject
                    {
                        ["type"] = "string",
                        ["enum"] = new JsonArray("academic", "preference", "engagement", "goal_related"),
                    },
                    ["content"] = new JsonObject
                    {
                        ["type"] = "string",
                        ["description"] = "One or two sentences describing the fact.",
                    },
                },
                ["required"] = new JsonArray("category", "content"),
            },
        },
        new ClaudeToolDefinition
        {
            Name = UpdateGoal,
            Description =
                "Create or update a learning goal for the learner. If a goal " +
                "with a matching title already exists, it is updated; " +
                "otherwise a new one is created.",
            InputSchema = new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    ["title"] = new JsonObject { ["type"] = "string" },
                    ["description"] = new JsonObject { ["type"] = "string" },
                    ["status"] = new JsonObject
                    {
                        ["type"] = "string",
                        ["enum"] = new JsonArray("active", "completed", "abandoned"),
                    },
                },
                ["required"] = new JsonArray("title", "status"),
            },
        },
    ];
}
