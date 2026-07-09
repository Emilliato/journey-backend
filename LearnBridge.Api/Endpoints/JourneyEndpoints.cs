using LearnBridge.Api.Auditing;
using LearnBridge.Api.Authorization;
using LearnBridge.Api.Consent;
using LearnBridge.Api.Features.Journey;
using LearnBridge.Data;
using LearnBridge.Data.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace LearnBridge.Api.Endpoints;

/// <summary>
/// The Claude Proxy surface — see docs/ARCHITECTURE.md. The Anthropic API
/// key never reaches the client; the Angular chat UI only ever talks to
/// these three endpoints.
/// </summary>
public static class JourneyEndpoints
{
    public static void MapJourneyEndpoints(this WebApplication app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/journey").WithTags("Journey").RequireAuthorization();

        group.MapPost("/sessions", StartSessionAsync).WithName("StartJourneySession");
        group.MapPost("/sessions/{sessionId:guid}/messages", SendMessageAsync).WithName("SendJourneyMessage");
        group.MapPost("/sessions/{sessionId:guid}/complete", CompleteSessionAsync).WithName("CompleteJourneySession");
    }

    private static async Task<IResult> StartSessionAsync(
        StartSessionRequest request,
        HttpContext httpContext,
        LearnBridgeDbContext dbContext,
        ConsentGate consentGate,
        IJourneySessionStore sessionStore,
        IAuthorizationService authorizationService)
    {
        Guid? parentId = httpContext.User.GetParentId();

        if (parentId is null)
        {
            return Results.Unauthorized();
        }

        Learner? learner = await dbContext.Learners.FirstOrDefaultAsync(l => l.Id == request.LearnerId);

        if (learner is null)
        {
            return Results.NotFound();
        }

        AuthorizationResult authResult = await authorizationService.AuthorizeAsync(
            httpContext.User, learner, "LearnerDataAccess");

        if (!authResult.Succeeded)
        {
            return Results.Forbid();
        }

        if (!await consentGate.IsActiveAsync(learner.Id))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["consent"] = ["Parental consent for this learner is not active."],
            });
        }

        ConversationSession session = new() { LearnerId = learner.Id, WasOffline = false };
        dbContext.ConversationSessions.Add(session);
        await dbContext.SaveChangesAsync();

        sessionStore.Create(session.Id, learner.Id, parentId.Value);

        httpContext.MarkLearnerAccess(learner.Id, "conversation_sessions");

        return Results.Created(
            $"/api/journey/sessions/{session.Id}",
            new StartSessionResponse(session.Id, session.StartedAt));
    }

    private static async Task<IResult> SendMessageAsync(
        Guid sessionId,
        SendMessageRequest request,
        HttpContext httpContext,
        IJourneySessionStore sessionStore,
        JourneyConversationService conversationService,
        CancellationToken cancellationToken)
    {
        Guid? parentId = httpContext.User.GetParentId();
        JourneySessionState? session = sessionStore.Get(sessionId);

        if (session is null || parentId is null || session.ParentId != parentId.Value)
        {
            return Results.NotFound();
        }

        if (string.IsNullOrWhiteSpace(request.Message))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["message"] = ["Message is required."],
            });
        }

        SendMessageResponse response = await conversationService.SendMessageAsync(
            session, sessionId, request.Message, cancellationToken);

        httpContext.MarkLearnerAccess(session.LearnerId, "conversation_sessions");

        return Results.Ok(response);
    }

    private static async Task<IResult> CompleteSessionAsync(
        Guid sessionId,
        HttpContext httpContext,
        LearnBridgeDbContext dbContext,
        IJourneySessionStore sessionStore)
    {
        Guid? parentId = httpContext.User.GetParentId();
        JourneySessionState? session = sessionStore.Get(sessionId);

        if (session is null || parentId is null || session.ParentId != parentId.Value)
        {
            return Results.NotFound();
        }

        ConversationSession? dbSession = await dbContext.ConversationSessions.FirstOrDefaultAsync(s => s.Id == sessionId);

        if (dbSession is not null)
        {
            dbSession.EndedAt = DateTime.UtcNow;
            dbSession.UpdatedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync();
        }

        sessionStore.Complete(sessionId);

        httpContext.MarkLearnerAccess(session.LearnerId, "conversation_sessions");

        return Results.NoContent();
    }
}
