using LearnBridge.Api.Authorization;
using LearnBridge.Domain.Features.BrainSparks;
using MediatR;
using Microsoft.AspNetCore.Authorization;

namespace LearnBridge.Api.Endpoints;

/// <summary>
/// Presentation for Brain Sparks. The bank listing is open to any authenticated
/// user; answering is learner-scoped (policy-checked here) and dispatched to a
/// handler that consent-gates and audits the journey_memory write.
/// </summary>
public static class BrainSparkEndpoints
{
    public static void MapBrainSparkEndpoints(this WebApplication app)
    {
        app.MapGet("/api/brainsparks", ListQuestionsAsync)
            .WithTags("BrainSparks")
            .WithName("ListBrainSparks")
            .RequireAuthorization();

        app.MapPost("/api/learners/{learnerId:guid}/brainsparks/answers", AnswerAsync)
            .WithTags("BrainSparks")
            .WithName("AnswerBrainSpark")
            .RequireAuthorization();
    }

    private static async Task<IResult> ListQuestionsAsync(ISender sender)
    {
        return Results.Ok(await sender.Send(new ListBrainSparksQuery()));
    }

    public sealed record AnswerRequest(string QuestionId, string Answer);

    private static async Task<IResult> AnswerAsync(
        Guid learnerId,
        AnswerRequest request,
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

        AnswerBrainSparkResult result = await sender.Send(
            new AnswerBrainSparkCommand(learnerId, request.QuestionId, request.Answer));

        return result.Status switch
        {
            AnswerBrainSparkStatus.InvalidAnswer => Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["answer"] = ["Unknown question or the answer is not one of its options."],
            }),
            AnswerBrainSparkStatus.LearnerNotFound => Results.NotFound(),
            AnswerBrainSparkStatus.ConsentInactive => Results.UnprocessableEntity(new
            {
                error = "Parental consent for this learner is not active, so nothing can be recorded.",
            }),
            _ => Results.Ok(result.Memory),
        };
    }
}
