using LearnBridge.Domain.Entities;
using Microsoft.AspNetCore.Authorization;

namespace LearnBridge.Api.Authorization;

/// <summary>
/// Succeeds when the authenticated parent owns the <see cref="Learner"/>
/// row being accessed directly (e.g. GET /api/learners/{id}) — no DB
/// lookup needed here since the resource already carries ParentId.
/// </summary>
public sealed class ParentOwnsLearnerDirectHandler
    : AuthorizationHandler<LearnerDataAccessRequirement, Learner>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        LearnerDataAccessRequirement requirement,
        Learner resource
    )
    {
        string? parentIdClaim = context.User.FindFirst(LearnBridgeClaimTypes.ParentId)?.Value;

        if (Guid.TryParse(parentIdClaim, out Guid parentId) && resource.ParentId == parentId)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
