namespace LearnBridge.Domain.Entities;

public enum AuditAction
{
    Read,
    Write,
}

/// <summary>
/// One row per read or write to any learner-linked table, per CLAUDE.md
/// constraint 5. Written by <c>AuditLoggingMiddleware</c> — see that class
/// for how requests get attributed to a learner and an actor.
/// </summary>
public sealed class AccessAuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid LearnerId { get; set; }

    /// <summary>The authenticated principal's user id (always a parent — see ApplicationUser).</summary>
    public Guid ActorId { get; set; }

    public AuditAction Action { get; set; }

    /// <summary>Which table/resource was touched, e.g. "learning_profile", "goals".</summary>
    public string Resource { get; set; } = string.Empty;

    public string RequestPath { get; set; } = string.Empty;

    public int ResponseStatusCode { get; set; }

    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
}
