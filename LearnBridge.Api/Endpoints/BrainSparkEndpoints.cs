using LearnBridge.Api.Auditing;
using LearnBridge.Api.Consent;
using LearnBridge.Api.Features.BrainSparks;
using LearnBridge.Data;
using LearnBridge.Data.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace LearnBridge.Api.Endpoints;

/// <summary>
/// Brain Sparks: quick one-tap questions that teach JOURNEY how a learner
/// likes to learn. The question bank is server-curated (constraint 4 —
/// learning preferences and engagement only, never feelings/health/family)
/// and an answer is a journey_memory write, so it is consent-gated
/// server-side (constraint 2) and audited (constraint 5) like any other.
/// </summary>
public static class BrainSparkEndpoints
{
    public static void MapBrainSparkEndpoints(this WebApplication app)
    {
        app.MapGet("/api/brainsparks", ListQuestions)
            .WithTags("BrainSparks")
            .WithName("ListBrainSparks")
            .RequireAuthorization();

        app.MapPost("/api/learners/{learnerId:guid}/brainsparks/answers", AnswerAsync)
            .WithTags("BrainSparks")
            .WithName("AnswerBrainSpark")
            .RequireAuthorization();
    }

    private static IResult ListQuestions()
    {
        var response = BrainSparkQuestionBank.Questions.Select(q => new
        {
            q.Id,
            q.Kind,
            q.Prompt,
            q.Options,
            Category = q.Category == JourneyMemoryCategory.Preference ? "preference" : "engagement",
        });

        return Results.Ok(response);
    }

    public sealed record AnswerRequest(string QuestionId, string Answer);

    private static async Task<IResult> AnswerAsync(
        Guid learnerId,
        AnswerRequest request,
        HttpContext httpContext,
        LearnBridgeDbContext dbContext,
        IAuthorizationService authorizationService,
        ConsentGate consentGate)
    {
        BrainSparkQuestion? question = BrainSparkQuestionBank.Find(request.QuestionId ?? string.Empty);

        if (question is null || !question.Options.Contains(request.Answer, StringComparer.Ordinal))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["answer"] = ["Unknown question or the answer is not one of its options."],
            });
        }

        Learner? learner = await dbContext.Learners.FirstOrDefaultAsync(l => l.Id == learnerId);

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

        if (!await consentGate.IsActiveAsync(learnerId))
        {
            return Results.UnprocessableEntity(new
            {
                error = "Parental consent for this learner is not active, so nothing can be recorded.",
            });
        }

        JourneyMemory memory = new()
        {
            LearnerId = learnerId,
            Category = question.Category,
            Content = BrainSparkQuestionBank.MemoryContent(question, request.Answer),
        };

        dbContext.JourneyMemories.Add(memory);
        await dbContext.SaveChangesAsync();

        httpContext.MarkLearnerAccess(learnerId, "journey_memory");

        return Results.Ok(new
        {
            memory.Id,
            Category = question.Category == JourneyMemoryCategory.Preference ? "preference" : "engagement",
            memory.Content,
            memory.CreatedAt,
        });
    }
}
