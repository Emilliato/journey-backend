using LearnBridge.Domain.Entities;
using MediatR;

namespace LearnBridge.Domain.Features.BrainSparks;

public sealed record BrainSparkDto(
    string Id,
    string Kind,
    string Prompt,
    IReadOnlyList<string> Options,
    string Category);

/// <summary>Lists the server-curated Brain Spark question bank. No data access.</summary>
public sealed record ListBrainSparksQuery : IRequest<IReadOnlyList<BrainSparkDto>>;

internal sealed class ListBrainSparksQueryHandler
    : IRequestHandler<ListBrainSparksQuery, IReadOnlyList<BrainSparkDto>>
{
    public Task<IReadOnlyList<BrainSparkDto>> Handle(ListBrainSparksQuery request, CancellationToken cancellationToken)
    {
        IReadOnlyList<BrainSparkDto> result = BrainSparkQuestionBank.Questions
            .Select(q => new BrainSparkDto(
                q.Id,
                q.Kind,
                q.Prompt,
                q.Options,
                q.Category == JourneyMemoryCategory.Preference ? "preference" : "engagement"))
            .ToList();

        return Task.FromResult(result);
    }
}
