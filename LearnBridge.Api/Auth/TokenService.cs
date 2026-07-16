using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using LearnBridge.Api.Authorization;
using LearnBridge.Domain.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace LearnBridge.Api.Auth;

/// <summary>
/// Issues JWTs for parent and learner accounts. Parent tokens carry the
/// account id as the "sub" claim (read by GetParentId / the parent-side
/// authorization handlers). Learner tokens additionally carry the
/// learner_id claim, which is what LearnerOwnDataHandler matches against —
/// a learner token never authorizes as a parent because its "sub" is the
/// learner's own user id, which matches no Learners.ParentId row.
/// </summary>
public sealed class TokenService : ITokenService
{
    private readonly JwtOptions _options;

    public TokenService(IOptions<JwtOptions> options)
    {
        _options = options.Value;
    }

    public (string Token, DateTime ExpiresAt) CreateToken(Guid userId, string? email, Guid? learnerId = null)
    {
        DateTime expiresAt = DateTime.UtcNow.AddDays(_options.ExpiryDays);

        List<Claim> claims =
        [
            new Claim(LearnBridgeClaimTypes.ParentId, userId.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, email ?? string.Empty),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(LearnBridgeClaimTypes.Role, learnerId is null ? LearnBridgeRoles.Parent : LearnBridgeRoles.Learner),
        ];

        if (learnerId is not null)
        {
            claims.Add(new Claim(LearnBridgeClaimTypes.LearnerId, learnerId.Value.ToString()));
        }

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
