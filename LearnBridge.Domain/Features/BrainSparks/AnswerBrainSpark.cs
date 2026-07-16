using LearnBridge.Domain.Abstractions;
using LearnBridge.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LearnBridge.Domain.Features.BrainSparks;

public enum AnswerBrainSparkStatus
{
    InvalidAnswer,
    LearnerNotFound,
    ConsentInactive,
    Recorded,
}

public sealed record AnswerBrainSparkMemoryDto(Guid Id, string Category, string Content, DateTime CreatedAt);

public sealed record AnswerBrainSparkResult(AnswerBrainSparkStatus Status, AnswerBrainSparkMemoryDto? Memory = null);

/// <summary>
/// Records a Brain Spark answer as a journey_memory row. Consent-gated
/// server-side (constraint 2) — the write is refused when consent is inactive
/// — and audited (constraint 5). Category is always Preference or Engagement
/// (constraint 4), fixed by the question bank.
/// </summary>
public sealed record AnswerBrainSparkCommand(Guid LearnerId, string? QuestionId, string Answer)
    : IRequest<AnswerBrainSparkResult>;

internal sealed class AnswerBrainSparkCommandHandler
    : IRequestHandler<AnswerBrainSparkCommand, AnswerBrainSparkResult>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ConsentGate _consentGate;
    private readonly IAuditContext _auditContext;

    public AnswerBrainSparkCommandHandler(
        IApplicationDbContext dbContext, ConsentGate consentGate, IAuditContext auditContext)
    {
        _dbContext = dbContext;
        _consentGate = consentGate;
        _auditContext = auditContext;
    }

    public async Task<AnswerBrainSparkResult> Handle(AnswerBrainSparkCommand request, CancellationToken cancellationToken)
    {
        BrainSparkQuestion? question = BrainSparkQuestionBank.Find(request.QuestionId ?? string.Empty);

        if (question is null || !question.Options.Contains(request.Answer, StringComparer.Ordinal))
        {
            return new AnswerBrainSparkResult(AnswerBrainSparkStatus.InvalidAnswer);
        }

        bool exists = await _dbContext.Learners.AnyAsync(l => l.Id == request.LearnerId, cancellationToken);

        if (!exists)
        {
            return new AnswerBrainSparkResult(AnswerBrainSparkStatus.LearnerNotFound);
        }

        if (!await _consentGate.IsActiveAsync(request.LearnerId, cancellationToken))
        {
            return new AnswerBrainSparkResult(AnswerBrainSparkStatus.ConsentInactive);
        }

        JourneyMemory memory = new()
        {
            LearnerId = request.LearnerId,
            Category = question.Category,
            Content = BrainSparkQuestionBank.MemoryContent(question, request.Answer),
        };

        _dbContext.JourneyMemories.Add(memory);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _auditContext.MarkLearnerAccess(request.LearnerId, "journey_memory");

        string category = question.Category == JourneyMemoryCategory.Preference ? "preference" : "engagement";

        return new AnswerBrainSparkResult(
            AnswerBrainSparkStatus.Recorded,
            new AnswerBrainSparkMemoryDto(memory.Id, category, memory.Content, memory.CreatedAt));
    }
}
