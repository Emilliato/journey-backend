using LearnBridge.Domain.Entities;
using LearnBridge.Data.Entities;

namespace LearnBridge.Api.Auth;

public interface ITokenService
{
    /// <summary>
    /// Issues a JWT for a parent account (no learnerId) or a learner
    /// account (learnerId set — the token then carries the learner_id
    /// claim that LearnerOwnDataHandler authorizes against).
    /// </summary>
    (string Token, DateTime ExpiresAt) CreateToken(ApplicationUser user, Guid? learnerId = null);
}
