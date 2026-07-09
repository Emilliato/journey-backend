namespace LearnBridge.Api.Authorization;

public static class LearnBridgeClaimTypes
{
    /// <summary>The authenticated parent's ApplicationUser id. Standard "sub" claim.</summary>
    public const string ParentId = "sub";

    /// <summary>
    /// A learner-scoped token's learner id. No token issuance flow exists
    /// yet — only parent accounts are wired up in Phase 1/2 (see
    /// CLAUDE.md: "ASP.NET Core Identity wired up (parent accounts)") — but
    /// the claim name and the handler that reads it are ready for if/when
    /// a learner-specific auth path is built.
    /// </summary>
    public const string LearnerId = "learner_id";
}
