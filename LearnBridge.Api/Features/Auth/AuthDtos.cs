namespace LearnBridge.Api.Features.Auth;

public sealed record RegisterRequest(string Email, string Password, string? DisplayName);

/// <summary>
/// Identifier is the parent's email or a learner's username — login
/// resolves either. (The property keeps the historical name "Email" on the
/// wire via the constructor parameter for client compatibility.)
/// </summary>
public sealed record LoginRequest(string Email, string Password);

/// <summary>
/// Role is "Parent" or "Learner". For learner logins, LearnerId identifies
/// the learner profile the account is bound to (the client routes straight
/// into that learner's JOURNEY); UserId is the Identity account id in both
/// cases.
/// </summary>
public sealed record AuthResponse(
    string Token,
    DateTime ExpiresAt,
    Guid ParentId,
    string Email,
    string? DisplayName,
    string Role,
    Guid? LearnerId);
