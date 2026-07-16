using LearnBridge.Domain.Abstractions;
using LearnBridge.Domain.Entities;
using MediatR;

namespace LearnBridge.Domain.Features.Learners;

public enum CreateLearnerStatus
{
    DisplayNameRequired,
    CredentialsRequired,
    ConsentRequired,
    IdentityError,
    Created,
}

public sealed record CreateLearnerResult(
    CreateLearnerStatus Status,
    LearnerResponse? Learner = null,
    IReadOnlyList<string>? Errors = null);

/// <summary>
/// Creates a child profile: the learner's own Identity account (Learner
/// role), the Learner row, and its founding parental_consent — no learner
/// ever exists without an active consent (constraint 2). The account is
/// created first; if the profile save fails, it is deleted so no orphan
/// sign-in remains.
/// </summary>
public sealed record CreateLearnerCommand(
    Guid ParentId,
    string DisplayName,
    string Username,
    string Password,
    bool ConsentGranted) : IRequest<CreateLearnerResult>;

internal sealed class CreateLearnerCommandHandler
    : IRequestHandler<CreateLearnerCommand, CreateLearnerResult>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IIdentityService _identityService;
    private readonly IAuditContext _auditContext;

    public CreateLearnerCommandHandler(
        IApplicationDbContext dbContext, IIdentityService identityService, IAuditContext auditContext)
    {
        _dbContext = dbContext;
        _identityService = identityService;
        _auditContext = auditContext;
    }

    public async Task<CreateLearnerResult> Handle(CreateLearnerCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.DisplayName))
        {
            return new CreateLearnerResult(CreateLearnerStatus.DisplayNameRequired);
        }

        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
        {
            return new CreateLearnerResult(CreateLearnerStatus.CredentialsRequired);
        }

        if (!request.ConsentGranted)
        {
            return new CreateLearnerResult(CreateLearnerStatus.ConsentRequired);
        }

        IdentityCreateResult account = await _identityService.CreateLearnerAccountAsync(
            request.Username, request.Password, request.DisplayName, cancellationToken);

        if (!account.Succeeded)
        {
            return new CreateLearnerResult(CreateLearnerStatus.IdentityError, Errors: account.Errors);
        }

        Learner learner = new()
        {
            ParentId = request.ParentId,
            DisplayName = request.DisplayName,
            UserId = account.UserId,
        };

        ParentalConsent consent = new()
        {
            LearnerId = learner.Id,
            ParentId = request.ParentId,
        };

        _dbContext.Learners.Add(learner);
        _dbContext.ParentalConsents.Add(consent);

        try
        {
            // One SaveChanges — the learner and its founding consent land
            // together, or neither does.
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            // Roll back the orphaned sign-in the profile would have belonged to.
            await _identityService.DeleteUserAsync(account.UserId, cancellationToken);
            throw;
        }

        _auditContext.MarkLearnerAccess(learner.Id, "learners");

        return new CreateLearnerResult(
            CreateLearnerStatus.Created,
            new LearnerResponse(learner.Id, learner.DisplayName, learner.CreatedAt, consent.IsActive, learner.AvatarConfig));
    }
}
