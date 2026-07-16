using LearnBridge.Domain.Abstractions;

namespace LearnBridge.Api.Auditing;

/// <summary>
/// Presentation-layer implementation of <see cref="IAuditContext"/>: records
/// marked accesses on the current HTTP request (via <see cref="AuditContextExtensions"/>),
/// which <see cref="AuditLoggingMiddleware"/> flushes to access_audit_log once
/// the response has finished. Scoped to the request.
/// </summary>
public sealed class AuditContext : IAuditContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuditContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public void MarkLearnerAccess(Guid learnerId, string resource)
    {
        HttpContext context = _httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("Audit marking requires an active HTTP context.");

        context.MarkLearnerAccess(learnerId, resource);
    }
}
