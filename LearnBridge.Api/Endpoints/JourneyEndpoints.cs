using LearnBridge.Api.Authorization;
using LearnBridge.Domain.Features.Journey;
using MediatR;
using Microsoft.AspNetCore.Authorization;

namespace LearnBridge.Api.Endpoints;

/// <summary>
/// The Claude Proxy surface — see docs/ARCHITECTURE.md. The Anthropic API key
/// never reaches the client; the Angular chat UI only ever talks to these three
/// endpoints. Thin: each resolves the caller and dispatches a command.
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
        ISender sender,
        IAuthorizationService authorizationService)
    {
        Guid? parentId = httpContext.User.GetParentId();

        if (parentId is null)
        {
            return Results.Unauthorized();
        }

        AuthorizationResult authResult = await authorizationService.AuthorizeAsync(
            httpContext.User, new LearnerScopedResource(request.LearnerId), "LearnerDataAccess");

        if (!authResult.Succeeded)
        {
            return Results.Forbid();
        }

        StartSessionResult result = await sender.Send(new StartSessionCommand(request.LearnerId, parentId.Value));

        return result.Status switch
        {
            StartSessionStatus.LearnerNotFound => Results.NotFound(),
            StartSessionStatus.ConsentInactive => Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["consent"] = ["Parental consent for this learner is not active."],
            }),
            _ => Results.Created($"/api/journey/sessions/{result.Response!.SessionId}", result.Response),
        };
    }

    private static async Task<IResult> SendMessageAsync(
        Guid sessionId,
        SendMessageRequest request,
        HttpContext httpContext,
        ISender sender)
    {
        Guid? parentId = httpContext.User.GetParentId();

        if (parentId is null)
        {
            return Results.NotFound();
        }

        SendMessageOutcome outcome = await sender.Send(
            new SendMessageCommand(sessionId, parentId.Value, request.Message));

        return outcome.Status switch
        {
            SendMessageStatus.SessionNotFound => Results.NotFound(),
            SendMessageStatus.MessageRequired => Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["message"] = ["Message is required."],
            }),
            _ => Results.Ok(outcome.Response),
        };
    }

    private static async Task<IResult> CompleteSessionAsync(
        Guid sessionId,
        HttpContext httpContext,
        ISender sender)
    {
        Guid? parentId = httpContext.User.GetParentId();

        if (parentId is null)
        {
            return Results.NotFound();
        }

        bool completed = await sender.Send(new CompleteSessionCommand(sessionId, parentId.Value));

        return completed ? Results.NoContent() : Results.NotFound();
    }
}
