using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace fletflow.Infrastructure.Security
{
    public class JwtTokenService
    {
        private readonly JwtSettings _jwtSettings;

        public JwtTokenService(IOptions<JwtSettings> jwtOptions)
        {
            _jwtSettings = jwtOptions.Value;
        }

        public string GenerateToken(Guid userId, string username, string email, string role, Guid ownerUserId, int tokenVersion = 0)
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Key));

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.NameId, userId.ToString()),
                new Claim(JwtRegisteredClaimNames.UniqueName, username),
                new Claim(JwtRegisteredClaimNames.Email, email),
                new Claim("owner_id", ownerUserId.ToString()),
                new Claim("token_version", tokenVersion.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                new Claim(ClaimTypes.Role, role.Trim())
            };

            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var issuedAt = DateTime.UtcNow;

            var token = new JwtSecurityToken(
                issuer: _jwtSettings.Issuer,
                audience: _jwtSettings.Audience,
                claims: claims,
                notBefore: issuedAt,
                expires: issuedAt.AddMinutes(_jwtSettings.ExpireMinutes),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
