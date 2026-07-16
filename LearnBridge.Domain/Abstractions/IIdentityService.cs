namespace LearnBridge.Domain.Abstractions;

/// <summary>Minimal projection of an Identity account the application layer needs.</summary>
public sealed record IdentityAccount(Guid UserId, string? Email, string? UserName, string? DisplayName);

public sealed record IdentityCreateResult(bool Succeeded, Guid UserId, IReadOnlyList<string> Errors);

/// <summary>
/// The port over ASP.NET Core Identity so application handlers can create and
/// authenticate accounts without depending on UserManager/ApplicationUser
/// (infrastructure). Implemented in the presentation/infrastructure layer.
/// </summary>
public interface IIdentityService
{
    Task<IdentityCreateResult> CreateParentAsync(string email, string password, string? displayName, CancellationToken cancellationToken);

    Task<IdentityCreateResult> CreateLearnerAccountAsync(string username, string password, string displayName, CancellationToken cancellationToken);

    Task DeleteUserAsync(Guid userId, CancellationToken cancellationToken);

    Task<IdentityAccount?> FindByEmailOrUserNameAsync(string identifier, CancellationToken cancellationToken);

    Task<bool> CheckPasswordAsync(Guid userId, string password, CancellationToken cancellationToken);
}
