using Microsoft.AspNetCore.Http;

namespace LearnBridge.Api.Auditing;

/// <summary>
/// How an endpoint tells <see cref="AuditLoggingMiddleware"/> which
/// learner(s) and resource a request touched. Call this as soon as the
/// handler knows which learner is involved — before or after the actual
/// read/write, it doesn't matter, since the middleware only writes audit
/// rows once the response has finished. Safe to call more than once per
/// request (e.g. a list endpoint touching several learners' rows) — one
/// audit row is written per call.
/// </summary>
public static class AuditContextExtensions
{
    private const string MarkedAccessesKey = "AuditMarkedAccesses";

    public static void MarkLearnerAccess(this HttpContext context, Guid learnerId, string resource)
    {
        GetOrCreateMarkedAccesses(context).Add((learnerId, resource));
    }

    public static IReadOnlyList<(Guid LearnerId, string Resource)> GetMarkedAccesses(this HttpContext context)
    {
        return context.Items.TryGetValue(MarkedAccessesKey, out object? value) &&
            value is List<(Guid LearnerId, string Resource)> marked
                ? marked
                : [];
    }

    private static List<(Guid LearnerId, string Resource)> GetOrCreateMarkedAccesses(HttpContext context)
    {
        if (context.Items.TryGetValue(MarkedAccessesKey, out object? value) &&
            value is List<(Guid LearnerId, string Resource)> existing)
        {
            return existing;
        }

        List<(Guid LearnerId, string Resource)> created = [];
        context.Items[MarkedAccessesKey] = created;

        return created;
    }
}
