using LearnBridge.Data.Entities;

namespace LearnBridge.Api.Auth;

public interface ITokenService
{
    (string Token, DateTime ExpiresAt) CreateToken(ApplicationUser user);
}
