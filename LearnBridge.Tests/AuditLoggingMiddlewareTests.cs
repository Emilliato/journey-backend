using System.Security.Claims;
using LearnBridge.Api.Auditing;
using LearnBridge.Api.Authorization;
using LearnBridge.Data;
using LearnBridge.Domain.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LearnBridge.Tests;

public class AuditLoggingMiddlewareTests
{
    private static LearnBridgeDbContext CreateDbContext()
    {
        DbContextOptions<LearnBridgeDbContext> options = new DbContextOptionsBuilder<LearnBridgeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new LearnBridgeDbContext(options);
    }

    [Fact]
    public async Task WritesAuditRow_WhenEndpointMarksLearnerAccess()
    {
        Guid learnerId = Guid.NewGuid();
        Guid parentId = Guid.NewGuid();

        await using LearnBridgeDbContext dbContext = CreateDbContext();

        DefaultHttpContext httpContext = new();
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(LearnBridgeClaimTypes.ParentId, parentId.ToString())],
            "TestAuth"
        ));
        httpContext.Request.Method = "GET";
        httpContext.Request.Path = "/api/learners/" + learnerId;
        httpContext.Response.StatusCode = 200;

        AuditLoggingMiddleware middleware = new(next: ctx =>
        {
            // Simulates what an endpoint handler will do once one exists —
            // see AuditContextExtensions.
            ctx.MarkLearnerAccess(learnerId, "learners");
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(httpContext, dbContext);

        AccessAuditLog logEntry = Assert.Single(dbContext.AccessAuditLogs);
        Assert.Equal(learnerId, logEntry.LearnerId);
        Assert.Equal(parentId, logEntry.ActorId);
        Assert.Equal(AuditAction.Read, logEntry.Action);
        Assert.Equal("learners", logEntry.Resource);
        Assert.Equal(200, logEntry.ResponseStatusCode);
    }

    [Fact]
    public async Task WritesAction_AsWrite_ForNonGetRequests()
    {
        Guid learnerId = Guid.NewGuid();

        await using LearnBridgeDbContext dbContext = CreateDbContext();

        DefaultHttpContext httpContext = new();
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity());
        httpContext.Request.Method = "POST";

        AuditLoggingMiddleware middleware = new(next: ctx =>
        {
            ctx.MarkLearnerAccess(learnerId, "goals");
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(httpContext, dbContext);

        AccessAuditLog logEntry = Assert.Single(dbContext.AccessAuditLogs);
        Assert.Equal(AuditAction.Write, logEntry.Action);
    }

    [Fact]
    public async Task WritesOneRowPerLearner_WhenMultipleLearnersMarkedInOneRequest()
    {
        Guid learnerOneId = Guid.NewGuid();
        Guid learnerTwoId = Guid.NewGuid();

        await using LearnBridgeDbContext dbContext = CreateDbContext();

        DefaultHttpContext httpContext = new();
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity());
        httpContext.Request.Method = "GET";

        AuditLoggingMiddleware middleware = new(next: ctx =>
        {
            // Simulates a list endpoint (e.g. GET /api/learners) touching
            // more than one learner's row in a single request.
            ctx.MarkLearnerAccess(learnerOneId, "learners");
            ctx.MarkLearnerAccess(learnerTwoId, "learners");
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(httpContext, dbContext);

        Assert.Equal(2, dbContext.AccessAuditLogs.Count());
        Assert.Contains(dbContext.AccessAuditLogs, l => l.LearnerId == learnerOneId);
        Assert.Contains(dbContext.AccessAuditLogs, l => l.LearnerId == learnerTwoId);
    }

    [Fact]
    public async Task WritesNothing_WhenRequestNeverTouchesLearnerData()
    {
        await using LearnBridgeDbContext dbContext = CreateDbContext();

        DefaultHttpContext httpContext = new();
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity());

        AuditLoggingMiddleware middleware = new(next: _ => Task.CompletedTask);

        await middleware.InvokeAsync(httpContext, dbContext);

        Assert.Empty(dbContext.AccessAuditLogs);
    }
}
