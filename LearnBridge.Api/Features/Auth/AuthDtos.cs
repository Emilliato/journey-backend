namespace LearnBridge.Api.Features.Auth;

public sealed record RegisterRequest(string Email, string Password, string? DisplayName);

public sealed record LoginRequest(string Email, string Password);

public sealed record AuthResponse(string Token, DateTime ExpiresAt, Guid ParentId, string Email, string? DisplayName);
