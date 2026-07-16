using System.Security.Claims;
using LearnBridge.Api.Authorization;
using LearnBridge.Data;
using LearnBridge.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LearnBridge.Tests;

public class AuthorizationHandlerTests
{
    private static LearnBridgeDbContext CreateDbContext()
    {
        DbContextOptions<LearnBridgeDbContext> options = new DbContextOptionsBuilder<LearnBridgeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new LearnBridgeDbContext(options);
    }

    private static ClaimsPrincipal PrincipalWithClaim(string type, string value)
    {
        ClaimsIdentity identity = new([new Claim(type, value)], "TestAuth");
        return new ClaimsPrincipal(identity);
    }

    [Fact]
    public async Task ParentOwnsLearnerHandler_Succeeds_WhenParentOwnsTheLearner()
    {
        Guid parentId = Guid.NewGuid();
        Guid learnerId = Guid.NewGuid();

        await using LearnBridgeDbContext dbContext = CreateDbContext();
        dbContext.Learners.Add(new Learner { Id = learnerId, ParentId = parentId, DisplayName = "Test Learner" });
        await dbContext.SaveChangesAsync();

        Goal resource = new() { LearnerId = learnerId, Title = "Read 10 books" };
        LearnerDataAccessRequirement requirement = new();

        AuthorizationHandlerContext context = new(
            [requirement],
            PrincipalWithClaim(LearnBridgeClaimTypes.ParentId, parentId.ToString()),
            resource
        );

        await new ParentOwnsLearnerHandler(dbContext).HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task ParentOwnsLearnerHandler_Fails_WhenParentDoesNotOwnTheLearner()
    {
        Guid actualParentId = Guid.NewGuid();
        Guid otherParentId = Guid.NewGuid();
        Guid learnerId = Guid.NewGuid();

        await using LearnBridgeDbContext dbContext = CreateDbContext();
        dbContext.Learners.Add(new Learner { Id = learnerId, ParentId = actualParentId, DisplayName = "Test Learner" });
        await dbContext.SaveChangesAsync();

        Goal resource = new() { LearnerId = learnerId, Title = "Read 10 books" };
        LearnerDataAccessRequirement requirement = new();

        AuthorizationHandlerContext context = new(
            [requirement],
            PrincipalWithClaim(LearnBridgeClaimTypes.ParentId, otherParentId.ToString()),
            resource
        );

        await new ParentOwnsLearnerHandler(dbContext).HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task ParentOwnsLearnerDirectHandler_Succeeds_ForOwnLearnerRow()
    {
        Guid parentId = Guid.NewGuid();
        Learner resource = new() { ParentId = parentId, DisplayName = "Test Learner" };
        LearnerDataAccessRequirement requirement = new();

        AuthorizationHandlerContext context = new(
            [requirement],
            PrincipalWithClaim(LearnBridgeClaimTypes.ParentId, parentId.ToString()),
            resource
        );

        await new ParentOwnsLearnerDirectHandler().HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task LearnerOwnDataHandler_Succeeds_WhenLearnerIdClaimMatchesResource()
    {
        Guid learnerId = Guid.NewGuid();
        Goal resource = new() { LearnerId = learnerId, Title = "Read 10 books" };
        LearnerDataAccessRequirement requirement = new();

        AuthorizationHandlerContext context = new(
            [requirement],
            PrincipalWithClaim(LearnBridgeClaimTypes.LearnerId, learnerId.ToString()),
            resource
        );

        await new LearnerOwnDataHandler().HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task LearnerOwnDataHandler_Fails_WhenLearnerIdClaimIsForADifferentLearner()
    {
        Goal resource = new() { LearnerId = Guid.NewGuid(), Title = "Read 10 books" };
        LearnerDataAccessRequirement requirement = new();

        AuthorizationHandlerContext context = new(
            [requirement],
            PrincipalWithClaim(LearnBridgeClaimTypes.LearnerId, Guid.NewGuid().ToString()),
            resource
        );

        await new LearnerOwnDataHandler().HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task Requirement_Fails_WhenNeitherHandlerSucceeds_DefaultDeny()
    {
        // No matching claims at all — the "default-deny otherwise" case
        // from PLAN.md. Both handlers run; neither should succeed.
        Goal resource = new() { LearnerId = Guid.NewGuid(), Title = "Read 10 books" };
        LearnerDataAccessRequirement requirement = new();

        AuthorizationHandlerContext context = new(
            [requirement],
            new ClaimsPrincipal(new ClaimsIdentity()),
            resource
        );

        await new LearnerOwnDataHandler().HandleAsync(context);
        await using LearnBridgeDbContext dbContext = CreateDbContext();
        await new ParentOwnsLearnerHandler(dbContext).HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }
}
