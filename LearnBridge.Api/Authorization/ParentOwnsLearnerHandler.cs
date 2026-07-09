using LearnBridge.Data;
using LearnBridge.Data.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace LearnBridge.Api.Authorization;

/// <summary>
/// Succeeds when the authenticated parent owns the learner that a
/// learner-scoped resource (goal, journey memory, etc.) belongs to.
/// </summary>
public sealed class ParentOwnsLearnerHandler
    : AuthorizationHandler<LearnerDataAccessRequirement, ILearnerScoped>
{
    private readonly LearnBridgeDbContext _dbContext;

    public ParentOwnsLearnerHandler(LearnBridgeDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        LearnerDataAccessRequirement requirement,
        ILearnerScoped resource
    )
    {
        string? parentIdClaim = context.User.FindFirst(LearnBridgeClaimTypes.ParentId)?.Value;

        if (!Guid.TryParse(parentIdClaim, out Guid parentId))
        {
            return;
        }

        bool owns = await _dbContext.Learners.AnyAsync(
            l => l.Id == resource.LearnerId && l.ParentId == parentId
        );

        if (owns)
        {
            context.Succeed(requirement);
        }
    }
}
