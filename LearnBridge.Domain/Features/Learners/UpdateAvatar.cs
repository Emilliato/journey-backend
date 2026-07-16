using System.Text.Json;
using LearnBridge.Domain.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LearnBridge.Domain.Features.Learners;

public enum UpdateAvatarStatus
{
    InvalidConfig,
    NotFound,
    Updated,
}

public sealed record UpdateAvatarResult(UpdateAvatarStatus Status, LearnerResponse? Learner = null);

/// <summary>
/// Avatar Studio save. The config is cosmetic (not learning data), so no
/// consent gate — but the write still audits. Validated as non-empty,
/// ≤ 4000 chars, and well-formed JSON.
/// </summary>
public sealed record UpdateAvatarCommand(Guid LearnerId, string AvatarConfig) : IRequest<UpdateAvatarResult>;

internal sealed class UpdateAvatarCommandHandler : IRequestHandler<UpdateAvatarCommand, UpdateAvatarResult>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IAuditContext _auditContext;

    public UpdateAvatarCommandHandler(IApplicationDbContext dbContext, IAuditContext auditContext)
    {
        _dbContext = dbContext;
        _auditContext = auditContext;
    }

    public async Task<UpdateAvatarResult> Handle(UpdateAvatarCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.AvatarConfig) || request.AvatarConfig.Length > 4000 || !IsValidJson(request.AvatarConfig))
        {
            return new UpdateAvatarResult(UpdateAvatarStatus.InvalidConfig);
        }

        var learner = await _dbContext.Learners
            .FirstOrDefaultAsync(l => l.Id == request.LearnerId, cancellationToken);

        if (learner is null)
        {
            return new UpdateAvatarResult(UpdateAvatarStatus.NotFound);
        }

        learner.AvatarConfig = request.AvatarConfig;
        learner.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        _auditContext.MarkLearnerAccess(learner.Id, "learners");

        bool consentActive = await _dbContext.ParentalConsents
            .AnyAsync(c => c.LearnerId == learner.Id && c.RevokedAt == null, cancellationToken);

        return new UpdateAvatarResult(
            UpdateAvatarStatus.Updated,
            new LearnerResponse(learner.Id, learner.DisplayName, learner.CreatedAt, consentActive, learner.AvatarConfig));
    }

    private static bool IsValidJson(string value)
    {
        try
        {
            using JsonDocument _ = JsonDocument.Parse(value);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
