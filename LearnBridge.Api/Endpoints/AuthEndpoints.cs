using LearnBridge.Api.Auth;
using LearnBridge.Api.Features.Auth;
using LearnBridge.Data.Entities;
using Microsoft.AspNetCore.Identity;

namespace LearnBridge.Api.Endpoints;

/// <summary>
/// Parent signup/login only — see CLAUDE.md constraint 1. Both endpoints
/// require connectivity by construction (they're plain HTTP calls against
/// this API); there is deliberately no offline path here for the Angular
/// client to fall back to.
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

        (string token, DateTime expiresAt) = tokenService.CreateToken(user);

        return Results.Created(
            $"/api/auth/users/{user.Id}",
            new AuthResponse(token, expiresAt, user.Id, user.Email!, user.DisplayName));
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        UserManager<ApplicationUser> userManager,
        ITokenService tokenService)
    {
        ApplicationUser? user = await userManager.FindByEmailAsync(request.Email);

        if (user is null || !await userManager.CheckPasswordAsync(user, request.Password))
        {
            return Results.Unauthorized();
        }

        (string token, DateTime expiresAt) = tokenService.CreateToken(user);

        return Results.Ok(new AuthResponse(token, expiresAt, user.Id, user.Email!, user.DisplayName));
    }
}
