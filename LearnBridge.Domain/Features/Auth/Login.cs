using LearnBridge.Domain.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LearnBridge.Domain.Features.Auth;

/// <summary>
/// Authenticates a parent (by email) or a learner (by the username their
/// parent chose) — the identifier resolves either. A learner login is one
/// whose account is linked from a Learner profile; its token carries the
/// learner_id claim that scopes every later request. Returns null when the
/// credentials don't match, which the endpoint maps to 401.
/// </summary>
public sealed record LoginCommand(string Identifier, string Password) : IRequest<AuthResponse?>;

internal sealed class LoginCommandHandler : IRequestHandler<LoginCommand, AuthResponse?>
{
    private readonly IIdentityService _identityService;
    private readonly IApplicationDbContext _dbContext;
    private readonly ITokenService _tokenService;

    public LoginCommandHandler(
        IIdentityService identityService, IApplicationDbContext dbContext, ITokenService tokenService)
    {
        _identityService = identityService;
        _dbContext = dbContext;
        _tokenService = tokenService;
    }

    public async Task<AuthResponse?> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        IdentityAccount? account = await _identityService.FindByEmailOrUserNameAsync(request.Identifier, cancellationToken);

        if (account is null || !await _identityService.CheckPasswordAsync(account.UserId, request.Password, cancellationToken))
        {
            return null;
        }

        var learner = await _dbContext.Learners
            .Where(l => l.UserId == account.UserId)
            .Select(l => new { l.Id, l.DisplayName })
            .FirstOrDefaultAsync(cancellationToken);

        (string token, DateTime expiresAt) = _tokenService.CreateToken(
            account.UserId, account.Email ?? account.UserName, learner?.Id);

        return new AuthResponse(
            token,
            expiresAt,
            account.UserId,
            account.Email ?? account.UserName ?? string.Empty,
            learner?.DisplayName ?? account.DisplayName,
            learner is null ? "Parent" : "Learner",
            learner?.Id);
    }
}
