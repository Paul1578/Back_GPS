using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Threading.Tasks;
using fletflow.Infrastructure.Security;

namespace fletflow.Api.Middleware
{
    public class JwtMiddleware : IMiddleware
    {
        private readonly JwtSettings _jwtSettings;
        private readonly ILogger<JwtMiddleware> _logger;

        public JwtMiddleware(IOptions<JwtSettings> jwtOptions, ILogger<JwtMiddleware> logger)
        {
            _jwtSettings = jwtOptions.Value;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context, RequestDelegate next)
        {
            var token = context.Request.Headers["Authorization"].FirstOrDefault()?.Split(" ").Last();
            if (!string.IsNullOrEmpty(token))
            {
                try
                {
                    var tokenHandler = new JwtSecurityTokenHandler();
                    var key = Encoding.UTF8.GetBytes(_jwtSettings.Key);
                    tokenHandler.ValidateToken(token, new TokenValidationParameters
                    {
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(key),
                        ValidateIssuer = true,
                        ValidIssuer = _jwtSettings.Issuer,
                        ValidateAudience = true,
                        ValidAudience = _jwtSettings.Audience,
                        ValidateLifetime = true,
                        ClockSkew = TimeSpan.Zero
                    }, out SecurityToken validatedToken);

                    var jwtToken = (JwtSecurityToken)validatedToken;
                    var claimsIdentity = new ClaimsIdentity(jwtToken.Claims, "jwt");
                    context.User = new ClaimsPrincipal(claimsIdentity);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[JwtMiddleware] Token JWT inválido o expirado");
                    // No setea usuario, la autorización fallará después si es requerida
                }
            }
            await next(context);
        }
    }
}
