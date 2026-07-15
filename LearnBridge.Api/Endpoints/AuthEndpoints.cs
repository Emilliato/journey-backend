using LearnBridge.Api.Auth;
using LearnBridge.Api.Authorization;
using LearnBridge.Api.Features.Auth;
using LearnBridge.Data;
using LearnBridge.Data.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace LearnBridge.Api.Endpoints;

/// <summary>
/// Signup is parent-only; learner accounts are created by the parent via
/// the learners endpoint. Login accepts a parent's email or a learner's
/// username and returns which role authenticated so the client can route
/// (parent -> profile picker, learner -> their JOURNEY chat). All endpoints
/// here require connectivity by construction (plain HTTP against this
/// API); offline continuation on a device that has logged in before is a
/// client concern — see CLAUDE.md constraint 1 (amended 2026-07-15).
/// </summary>
public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/auth").WithTags("Auth");

        group.MapPost("/register", RegisterAsync).WithName("Register");
        group.MapPost("/login", LoginAsync).WithName("Login");
    }

    private static async Task<IResult> RegisterAsync(
        RegisterRequest request,
        UserManager<ApplicationUser> userManager,
        ITokenService tokenService)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["email"] = ["Email and password are required."],
            });
        }

        ApplicationUser user = new()
        {
            UserName = request.Email,
            Email = request.Email,
            DisplayName = string.IsNullOrWhiteSpace(request.DisplayName) ? null : request.DisplayName,
        };

        IdentityResult result = await userManager.CreateAsync(user, request.Password);

        if (!result.Succeeded)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["identity"] = result.Errors.Select(e => e.Description).ToArray(),
            });
        }

        await userManager.AddToRoleAsync(user, LearnBridgeRoles.Parent);

        (string token, DateTime expiresAt) = tokenService.CreateToken(user);

        return Results.Created(
            $"/api/auth/users/{user.Id}",
            new AuthResponse(token, expiresAt, user.Id, user.Email!, user.DisplayName, LearnBridgeRoles.Parent, null));
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        UserManager<ApplicationUser> userManager,
        LearnBridgeDbContext dbContext,
        ITokenService tokenService)
    {
        // Parents sign in with email; learners with the username their
        // parent chose. Try both — usernames and emails share Identity's
        // uniqueness guarantees, so the first match is the account.
        ApplicationUser? user = await userManager.FindByEmailAsync(request.Email)
            ?? await userManager.FindByNameAsync(request.Email);

        if (user is null || !await userManager.CheckPasswordAsync(user, request.Password))
        {
            return Results.Unauthorized();
        }

        // A learner account is one linked from a Learner profile. The
        // learner_id claim this puts on the token is what scopes every
        // subsequent request to that learner's own rows (LearnerOwnDataHandler).
        Learner? learner = await dbContext.Learners.FirstOrDefaultAsync(l => l.UserId == user.Id);

        (string token, DateTime expiresAt) = tokenService.CreateToken(user, learner?.Id);

        return Results.Ok(new AuthResponse(
            token,
            expiresAt,
            user.Id,
            user.Email ?? user.UserName ?? string.Empty,
            learner?.DisplayName ?? user.DisplayName,
            learner is null ? LearnBridgeRoles.Parent : LearnBridgeRoles.Learner,
            learner?.Id));
    }
}
