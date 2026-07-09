using Microsoft.AspNetCore.Identity;

namespace LearnBridge.Data.Entities;

/// <summary>
/// A parent account. This *is* the "parents" table from PLAN.md — realized
/// through ASP.NET Core Identity's user store rather than a separate custom
/// table, since Identity already owns account/credential storage. Only
/// parents get Identity accounts; learners do not sign in independently
/// (see CLAUDE.md constraint 1 — no offline or standalone auth path for
/// learners; a parent's session scopes which learners they can act on).
/// </summary>
public sealed class ApplicationUser : IdentityUser<Guid>
{
    public string? DisplayName { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
