using LearnBridge.Domain.Features.Auth;
using MediatR;

namespace LearnBridge.Api.Endpoints;

/// <summary>
/// Signup is parent-only; learner accounts are created by the parent via the
/// learners endpoint. Login accepts a parent's email or a learner's username
/// and returns which role authenticated so the client can route. Thin: builds
/// a command and dispatches it.
/// </summary>
public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/auth").WithTags("Auth");

        group.MapPost("/register", RegisterAsync).WithName("Register");
        group.MapPost("/login", LoginAsync).WithName("Login");
    }

    private static async Task<IResult> RegisterAsync(RegisterRequest request, ISender sender)
    {
        RegisterParentResult result = await sender.Send(
            new RegisterParentCommand(request.Email, request.Password, request.DisplayName));

        return result.Status switch
        {
            RegisterParentStatus.InvalidInput => Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["email"] = ["Email and password are required."],
            }),
            RegisterParentStatus.IdentityError => Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["identity"] = result.Errors?.ToArray() ?? [],
            }),
            _ => Results.Created($"/api/auth/users/{result.Auth!.ParentId}", result.Auth),
        };
    }

    private static async Task<IResult> LoginAsync(LoginRequest request, ISender sender)
    {
        AuthResponse? response = await sender.Send(new LoginCommand(request.Email, request.Password));

        return response is null ? Results.Unauthorized() : Results.Ok(response);
    }
}
