using System.Security.Claims;

namespace LearnBridge.Api.Authorization;

public static class ClaimsPrincipalExtensions
{
    public static Guid? GetParentId(this ClaimsPrincipal principal)
    {
        string? claim = principal.FindFirst(LearnBridgeClaimTypes.ParentId)?.Value;

        return Guid.TryParse(claim, out Guid parentId) ? parentId : null;
    }
}
