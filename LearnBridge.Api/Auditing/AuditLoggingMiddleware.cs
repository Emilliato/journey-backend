using LearnBridge.Api.Authorization;
using LearnBridge.Data;
using LearnBridge.Data.Entities;
using Microsoft.AspNetCore.Http;

namespace LearnBridge.Api.Auditing;

/// <summary>
/// Writes one <see cref="AccessAuditLog"/> row per request that touched a
/// learner-linked resource, per CLAUDE.md constraint 5. Runs the rest of
/// the pipeline first so it can see both the final response status code
/// and whatever the endpoint marked via <see cref="AuditContextExtensions.MarkLearnerAccess"/> —
/// a request that never calls that (nothing learner-linked happened)
/// produces no audit row, which is correct, not a gap.
/// </summary>
public sealed class AuditLoggingMiddleware
{
    private readonly RequestDelegate _next;

    public AuditLoggingMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, LearnBridgeDbContext dbContext)
    {
        await _next(context);

        IReadOnlyList<(Guid LearnerId, string Resource)> markedAccesses = context.GetMarkedAccesses();

        if (markedAccesses.Count == 0)
        {
            return;
        }

        string? parentIdClaim = context.User.FindFirst(LearnBridgeClaimTypes.ParentId)?.Value;

        if (!Guid.TryParse(parentIdClaim, out Guid actorId))
        {
            // No resolvable actor (e.g. request never authenticated) — still
            // worth a row with an empty actor rather than silently dropping
            // the audit trail for a request that reached learner data.
            actorId = Guid.Empty;
        }

        AuditAction action = HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method)
            ? AuditAction.Read
            : AuditAction.Write;

        foreach ((Guid learnerId, string resource) in markedAccesses)
        {
            dbContext.AccessAuditLogs.Add(new AccessAuditLog
            {
                LearnerId = learnerId,
                ActorId = actorId,
                Action = action,
                Resource = resource,
                RequestPath = context.Request.Path,
                ResponseStatusCode = context.Response.StatusCode,
            });
        }

        await dbContext.SaveChangesAsync();
    }
}

public static class AuditLoggingMiddlewareExtensions
{
    public static IApplicationBuilder UseAuditLogging(this IApplicationBuilder app)
    {
        return app.UseMiddleware<AuditLoggingMiddleware>();
    }
}
