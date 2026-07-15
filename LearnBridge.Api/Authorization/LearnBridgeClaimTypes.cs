namespace LearnBridge.Api.Authorization;

public static class LearnBridgeClaimTypes
{
    /// <summary>The authenticated parent's ApplicationUser id. Standard "sub" claim.</summary>
    public const string ParentId = "sub";

    /// <summary>
    /// A learner-scoped token's learner id — issued by TokenService for
    /// learner-account logins and matched by LearnerOwnDataHandler.
    /// </summary>
    public const string LearnerId = "learner_id";

    /// <summary>Raw role claim ("Parent" or "Learner") — informational for clients.</summary>
    public const string Role = "role";
}

/// <summary>Identity role names, seeded at startup (see Program.cs).</summary>
public static class LearnBridgeRoles
{
    public const string Parent = "Parent";
    public const string Learner = "Learner";
}
