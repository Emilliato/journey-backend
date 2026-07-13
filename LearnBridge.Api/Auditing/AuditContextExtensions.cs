using Microsoft.AspNetCore.Http;

namespace LearnBridge.Api.Auditing;

/// <summary>
/// How an endpoint tells <see cref="AuditLoggingMiddleware"/> which learner
/// and resource a request touched. Call this as soon as the handler knows
/// which learner is involved — before or after the actual read/write, it
/// doesn't matter, since the middleware only writes the audit row once the
/// response has finished.
/// </summary>
public static class AuditContextExtensions
{
    private const string LearnerIdKey = "AuditLearnerId";
    private const string ResourceKey = "AuditResource";

    public static void MarkLearnerAccess(this HttpContext context, Guid learnerId, string resource)
    {
        context.Items[LearnerIdKey] = learnerId;
        context.Items[ResourceKey] = resource;
    }

    public static Guid? GetAuditLearnerId(this HttpContext context)
    {
        return context.Items.TryGetValue(LearnerIdKey, out object? value) && value is Guid learnerId
            ? learnerId
            : null;
    }

    public static string? GetAuditResource(this HttpContext context)
    {
        return context.Items.TryGetValue(ResourceKey, out object? value) ? value as string : null;
    }
}
