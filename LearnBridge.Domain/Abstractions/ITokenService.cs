namespace LearnBridge.Domain.Abstractions;

public interface ITokenService
{
    /// <summary>
    /// Issues a JWT for a parent account (no learnerId) or a learner account
    /// (learnerId set — the token then carries the learner_id claim that the
    /// learner-ownership authorization handler matches against).
    /// </summary>
    (string Token, DateTime ExpiresAt) CreateToken(Guid userId, string? email, Guid? learnerId = null);
}
