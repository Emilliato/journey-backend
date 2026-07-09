using LearnBridge.Data.Entities;
using Microsoft.AspNetCore.Authorization;

namespace LearnBridge.Api.Authorization;

/// <summary>
/// Succeeds when a learner-scoped token's own learner id matches the
/// resource being accessed. No learner token-issuance flow exists yet
/// (Phase 1/2 only wire up parent accounts) — this handler is ready for
/// when one does, and is inert until then since no token will ever carry
/// the "learner_id" claim.
/// </summary>
public sealed class LearnerOwnDataHandler
    : AuthorizationHandler<LearnerDataAccessRequirement, ILearnerScoped>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        LearnerDataAccessRequirement requirement,
        ILearnerScoped resource
    )
    {
        string? learnerIdClaim = context.User.FindFirst(LearnBridgeClaimTypes.LearnerId)?.Value;

        if (Guid.TryParse(learnerIdClaim, out Guid learnerId) && learnerId == resource.LearnerId)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
