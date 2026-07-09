using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using LearnBridge.Api.Authorization;
using LearnBridge.Data.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace LearnBridge.Api.Auth;

/// <summary>
/// Issues JWTs for parent accounts only — see ApplicationUser and CLAUDE.md
/// constraint 1. There is no learner-facing token issuance path.
/// </summary>
public sealed class TokenService : ITokenService
{
    private readonly JwtOptions _options;

    public TokenService(IOptions<JwtOptions> options)
    {
        _options = options.Value;
    }

    public (string Token, DateTime ExpiresAt) CreateToken(ApplicationUser user)
    {
        DateTime expiresAt = DateTime.UtcNow.AddDays(_options.ExpiryDays);

        List<Claim> claims =
        [
            new Claim(LearnBridgeClaimTypes.ParentId, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        ];

        SymmetricSecurityKey signingKey = new(Encoding.UTF8.GetBytes(_options.SigningKey));
        SigningCredentials credentials = new(signingKey, SecurityAlgorithms.HmacSha256);

        JwtSecurityToken token = new(
            claims: claims,
            expires: expiresAt,
            signingCredentials: credentials
        );

        string tokenString = new JwtSecurityTokenHandler().WriteToken(token);

        return (tokenString, expiresAt);
    }
}
