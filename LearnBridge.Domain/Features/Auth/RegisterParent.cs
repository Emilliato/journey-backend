using LearnBridge.Domain.Abstractions;
using MediatR;

namespace LearnBridge.Domain.Features.Auth;

public enum RegisterParentStatus
{
    InvalidInput,
    IdentityError,
    Created,
}

public sealed record RegisterParentResult(
    RegisterParentStatus Status,
    AuthResponse? Auth = null,
    IReadOnlyList<string>? Errors = null);

/// <summary>
/// Registers a parent account (parent-only signup — learner accounts are
/// created via the learners feature) and returns a signed session token.
/// </summary>
public sealed record RegisterParentCommand(string Email, string Password, string? DisplayName)
    : IRequest<RegisterParentResult>;

internal sealed class RegisterParentCommandHandler
    : IRequestHandler<RegisterParentCommand, RegisterParentResult>
{
    private readonly IIdentityService _identityService;
    private readonly ITokenService _tokenService;

    public RegisterParentCommandHandler(IIdentityService identityService, ITokenService tokenService)
    {
        _identityService = identityService;
        _tokenService = tokenService;
    }

    public async Task<RegisterParentResult> Handle(RegisterParentCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return new RegisterParentResult(RegisterParentStatus.InvalidInput);
        }

        IdentityCreateResult created = await _identityService.CreateParentAsync(
            request.Email, request.Password, request.DisplayName, cancellationToken);

        if (!created.Succeeded)
        {
            return new RegisterParentResult(RegisterParentStatus.IdentityError, Errors: created.Errors);
        }

        (string token, DateTime expiresAt) = _tokenService.CreateToken(created.UserId, request.Email, learnerId: null);

        string? displayName = string.IsNullOrWhiteSpace(request.DisplayName) ? null : request.DisplayName;

        return new RegisterParentResult(
            RegisterParentStatus.Created,
            new AuthResponse(token, expiresAt, created.UserId, request.Email, displayName, "Parent", null));
    }
}
