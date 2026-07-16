using LearnBridge.Api.Authorization;
using LearnBridge.Data.Entities;
using LearnBridge.Domain.Abstractions;
using Microsoft.AspNetCore.Identity;

namespace LearnBridge.Api.Auth;

/// <summary>
/// Infrastructure implementation of <see cref="IIdentityService"/> over ASP.NET
/// Core Identity's <see cref="UserManager{TUser}"/>. Keeps UserManager and
/// ApplicationUser out of the application layer — handlers speak in ids and
/// primitive results.
/// </summary>
public sealed class IdentityService : IIdentityService
{
    private readonly UserManager<ApplicationUser> _userManager;

    public IdentityService(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public async Task<IdentityCreateResult> CreateParentAsync(
        string email, string password, string? displayName, CancellationToken cancellationToken)
    {
        ApplicationUser user = new()
        {
            UserName = email,
            Email = email,
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? null : displayName,
        };

        return await CreateAsync(user, password, LearnBridgeRoles.Parent);
    }

    public async Task<IdentityCreateResult> CreateLearnerAccountAsync(
        string username, string password, string displayName, CancellationToken cancellationToken)
    {
        ApplicationUser user = new()
        {
            UserName = username.Trim(),
            DisplayName = displayName,
        };

        return await CreateAsync(user, password, LearnBridgeRoles.Learner);
    }

    private async Task<IdentityCreateResult> CreateAsync(ApplicationUser user, string password, string role)
    {
        IdentityResult result = await _userManager.CreateAsync(user, password);

        if (!result.Succeeded)
        {
            return new IdentityCreateResult(false, Guid.Empty, result.Errors.Select(e => e.Description).ToArray());
        }

        await _userManager.AddToRoleAsync(user, role);

        return new IdentityCreateResult(true, user.Id, []);
    }

    public async Task DeleteUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        ApplicationUser? user = await _userManager.FindByIdAsync(userId.ToString());

        if (user is not null)
        {
            await _userManager.DeleteAsync(user);
        }
    }

    public async Task<IdentityAccount?> FindByEmailOrUserNameAsync(string identifier, CancellationToken cancellationToken)
    {
        ApplicationUser? user = await _userManager.FindByEmailAsync(identifier)
            ?? await _userManager.FindByNameAsync(identifier);

        return user is null
            ? null
            : new IdentityAccount(user.Id, user.Email, user.UserName, user.DisplayName);
    }

    public async Task<bool> CheckPasswordAsync(Guid userId, string password, CancellationToken cancellationToken)
    {
        ApplicationUser? user = await _userManager.FindByIdAsync(userId.ToString());

        return user is not null && await _userManager.CheckPasswordAsync(user, password);
    }
}
