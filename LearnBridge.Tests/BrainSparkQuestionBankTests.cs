using LearnBridge.Domain.Features.BrainSparks;
using LearnBridge.Domain.Entities;
using Xunit;

namespace LearnBridge.Tests;

/// <summary>
/// Guards the Brain Spark boundary (CLAUDE.md constraint 4 applied to
/// question copy): questions are strictly about learning preferences,
/// interests, and engagement, and answers may only land in the Preference
/// or Engagement categories. If a test here fails because a question was
/// added, the question is the thing to fix — not the test.
/// </summary>
public class BrainSparkQuestionBankTests
{
    private static readonly string[] ForbiddenTerms =
    [
        "feel", "feeling", "mood", "emotion", "sad", "happy about your",
        "health", "sick", "family", "mom", "dad", "parent", "sister", "brother", "home life",
    ];

    [Fact]
    public void Questions_OnlyUsePreferenceOrEngagementCategories()
    {
        foreach (BrainSparkQuestion question in BrainSparkQuestionBank.Questions)
        {
            Assert.True(
                question.Category is JourneyMemoryCategory.Preference or JourneyMemoryCategory.Engagement,
                $"Question '{question.Id}' uses disallowed category {question.Category}.");
        }
    }

    [Fact]
    public void Questions_NeverAskAboutFeelingsHealthOrFamily()
    {
        foreach (BrainSparkQuestion question in BrainSparkQuestionBank.Questions)
        {
            string text = $"{question.Prompt} {string.Join(' ', question.Options)}".ToLowerInvariant();

            foreach (string term in ForbiddenTerms)
            {
                Assert.False(
                    text.Contains(term, StringComparison.OrdinalIgnoreCase),
                    $"Question '{question.Id}' contains forbidden term '{term}'.");
            }
        }
    }

    [Fact]
    public void Questions_HaveUniqueIdsAndAtLeastTwoOptions()
    {
        Assert.Equal(
            BrainSparkQuestionBank.Questions.Count,
            BrainSparkQuestionBank.Questions.Select(q => q.Id).Distinct(StringComparer.Ordinal).Count());

        Assert.All(BrainSparkQuestionBank.Questions, q => Assert.True(q.Options.Count >= 2));
    }

    [Fact]
    public void MemoryContent_IsDeterministicAndCarriesTheSparkPrefix()
    {
        BrainSparkQuestion question = BrainSparkQuestionBank.Questions[0];

        string content = BrainSparkQuestionBank.MemoryContent(question, question.Options[0]);

        Assert.StartsWith("Brain Spark — ", content);
        Assert.Contains(question.Prompt, content);
        Assert.Contains(question.Options[0], content);
    }
}
