using LearnBridge.Domain.Entities;

namespace LearnBridge.Api.Features.BrainSparks;

public sealed record BrainSparkQuestion(
    string Id,
    string Kind, // this_or_that | would_you_rather | poll
    string Prompt,
    IReadOnlyList<string> Options,
    JourneyMemoryCategory Category);

/// <summary>
/// The server-curated Brain Spark question bank. Hard boundary (CLAUDE.md
/// constraint 4): every question is strictly about learning preferences,
/// interests, and engagement — never feelings, mood, health, or family —
/// and answers may only land in the Preference or Engagement categories.
/// BrainSparkQuestionBankTests enforces both rules against this list.
/// </summary>
public static class BrainSparkQuestionBank
{
    public static readonly IReadOnlyList<BrainSparkQuestion> Questions =
    [
        new("spark-pictures-words", "this_or_that", "Pictures or words?",
            ["Pictures", "Words"], JourneyMemoryCategory.Preference),
        new("spark-puzzle-story", "would_you_rather", "Would you rather solve a puzzle or read a story?",
            ["Solve a puzzle", "Read a story"], JourneyMemoryCategory.Preference),
        new("spark-tonight-challenge", "poll", "Pick tonight's challenge:",
            ["Fractions ninja", "Spelling bee", "Space quiz"], JourneyMemoryCategory.Engagement),
        new("spark-learn-alone-together", "this_or_that", "Learning solo or with a buddy?",
            ["Solo", "With a buddy"], JourneyMemoryCategory.Preference),
        new("spark-short-long", "this_or_that", "Lots of quick questions or one big challenge?",
            ["Quick questions", "One big challenge"], JourneyMemoryCategory.Engagement),
        new("spark-topic-curious", "poll", "Which sounds most exciting right now?",
            ["Space", "Animals", "Inventions", "Oceans"], JourneyMemoryCategory.Preference),
        new("spark-explain-style", "would_you_rather", "Would you rather see a diagram or hear an example?",
            ["See a diagram", "Hear an example"], JourneyMemoryCategory.Preference),
        new("spark-practice-time", "poll", "When is learning easiest for you?",
            ["Right after school", "After dinner", "Weekend mornings"], JourneyMemoryCategory.Engagement),
    ];

    public static BrainSparkQuestion? Find(string id) =>
        Questions.FirstOrDefault(q => string.Equals(q.Id, id, StringComparison.Ordinal));

    /// <summary>
    /// The memory row content for an answer. Kept deterministic and shared
    /// with the Angular offline path, which composes the identical string
    /// when queueing an answer for sync.
    /// </summary>
    public static string MemoryContent(BrainSparkQuestion question, string answer) =>
        $"Brain Spark — {question.Prompt} → chose \"{answer}\"";
}
