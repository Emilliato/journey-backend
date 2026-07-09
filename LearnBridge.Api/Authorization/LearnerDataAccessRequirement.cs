using Microsoft.AspNetCore.Authorization;

namespace LearnBridge.Api.Authorization;

/// <summary>
/// Satisfied if EITHER of two independent handlers succeeds — ASP.NET
/// Core's authorization model treats multiple handlers registered for the
/// same requirement as OR, not AND. That's exactly "learner reads/writes
/// own rows, parent reads/writes their children's rows" from PLAN.md: two
/// different ownership paths to the same requirement. If neither handler
/// succeeds, the framework denies by default — no extra code needed for
/// "default-deny otherwise".
/// </summary>
public sealed class LearnerDataAccessRequirement : IAuthorizationRequirement
{
}
