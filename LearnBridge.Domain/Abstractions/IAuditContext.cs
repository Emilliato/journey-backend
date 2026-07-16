namespace LearnBridge.Domain.Abstractions;

/// <summary>
/// The audit port a handler uses to record that it touched a learner-linked
/// resource (CLAUDE.md constraint 5). The presentation layer implements it
/// over the current request, and the audit middleware writes one
/// access_audit_log row per marked access after the response completes.
/// Safe to call more than once per request.
/// </summary>
public interface IAuditContext
{
    void MarkLearnerAccess(Guid learnerId, string resource);
}
