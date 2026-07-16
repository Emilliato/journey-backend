using LearnBridge.Api.Authorization;
using LearnBridge.Domain.Features.Memory;
using MediatR;
using Microsoft.AspNetCore.Authorization;

namespace LearnBridge.Api.Endpoints;

/// <summary>
/// Presentation for the memory repository. Listing is learner-scoped
/// (policy-checked); deletion is parent-only — the requester's id is passed
/// to the handler, which requires it to own the learner, so a learner token
/// can never remove what a parent reviews.
/// </summary>
public static class MemoryEndpoints
{
    public static void MapMemoryEndpoints(this WebApplication app)
    {
        app.MapGet("/api/learners/{learnerId:guid}/memories", ListMemoriesAsync)
            .WithTags("Memories")
            .WithName("ListMemories")
            .RequireAuthorization();

        app.MapDelete("/api/learners/{learnerId:guid}/memories/{memoryId:guid}", DeleteMemoryAsync)
            .WithTags("Memories")
            .WithName("DeleteMemory")
            .RequireAuthorization();
    }

    private static async Task<IResult> ListMemoriesAsync(
        Guid learnerId,
        HttpContext httpContext,
        ISender sender,
        IAuthorizationService authorizationService)
    {
        AuthorizationResult authResult = await authorizationService.AuthorizeAsync(
            httpContext.User, new LearnerScopedResource(learnerId), "LearnerDataAccess");

        if (!authResult.Succeeded)
        {
            return Results.Forbid();
        }

        IReadOnlyList<MemoryDto>? memories = await sender.Send(new ListMemoriesQuery(learnerId));

        return memories is null ? Results.NotFound() : Results.Ok(memories);
    }

    private static async Task<IResult> DeleteMemoryAsync(
        Guid learnerId,
        Guid memoryId,
        HttpContext httpContext,
        ISender sender)
    {
        Guid? parentId = httpContext.User.GetParentId();

        if (parentId is null)
        {
            return Results.Unauthorized();
        }

        bool deleted = await sender.Send(new DeleteMemoryCommand(learnerId, memoryId, parentId.Value));

        return deleted ? Results.NoContent() : Results.NotFound();
    }
}
