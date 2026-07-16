using LearnBridge.Domain.Entities;
using Xunit;

namespace LearnBridge.Tests;

/// <summary>
/// Guards CLAUDE.md constraint 4: journey_memory.category is a closed set
/// of exactly four values. If this test starts failing because someone
/// added a member, that's the signal to go raise it explicitly first, not
/// to update the test.
/// </summary>
public class JourneyMemoryCategoryTests
{
    [Fact]
    public void Category_HasExactlyFourMembers()
    {
        Array values = Enum.GetValues(typeof(JourneyMemoryCategory));

        Assert.Equal(4, values.Length);
    }

    [Theory]
    [InlineData(JourneyMemoryCategory.Academic)]
    [InlineData(JourneyMemoryCategory.Preference)]
    [InlineData(JourneyMemoryCategory.Engagement)]
    [InlineData(JourneyMemoryCategory.GoalRelated)]
    public void Category_OnlyContainsApprovedValues(JourneyMemoryCategory category)
    {
        Assert.True(Enum.IsDefined(typeof(JourneyMemoryCategory), category));
    }
}
