using LearnBridge.Data.Entities;
using Microsoft.AspNetCore.Authorization;

namespace LearnBridge.Api.Authorization;

/// <summary>
/// Succeeds when a learner-scoped token's own learner id matches the
/// <see cref="Learner"/> row being accessed directly. Inert until a
/// learner token-issuance flow exists — see <see cref="LearnerOwnDataHandler"/>.
/// </summary>
public sealed class LearnerOwnDataDirectHandler
    : AuthorizationHandler<LearnerDataAccessRequirement, Learner>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        LearnerDataAccessRequirement requirement,
        Learner resource
    )
    {
        string? learnerIdClaim = context.User.FindFirst(LearnBridgeClaimTypes.LearnerId)?.Value;

        if (Guid.TryParse(learnerIdClaim, out Guid learnerId) && learnerId == resource.Id)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
