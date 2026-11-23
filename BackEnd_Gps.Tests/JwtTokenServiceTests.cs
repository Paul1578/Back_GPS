using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using fletflow.Infrastructure.Security;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Options;

namespace BackEnd_Gps.Tests;

public sealed class JwtTokenServiceTests
{
    [Fact]
    public void GenerateToken_UsesFifteenMinuteDefaultLifetime()
    {
        var service = new JwtTokenService(Options.Create(new JwtSettings
        {
            Key = "test-signing-key-with-at-least-32-bytes-long",
            Issuer = "fletflow.tests",
            Audience = "fletflow.tests"
        }));
        var beforeGeneration = DateTime.UtcNow;

        var userId = Guid.NewGuid();
        var raw = service.GenerateToken(
            userId, "driver", "driver@example.test", "Driver", Guid.NewGuid());

        var handler = new JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(raw);
        var lifetimeSeconds = (token.ValidTo - beforeGeneration).TotalSeconds;
        Assert.InRange(lifetimeSeconds, 895, 905);
        Assert.True(JwtAccessTokenLifetimePolicy.IsAllowed(token), $"Token ValidFrom={token.ValidFrom:o}; ValidTo={token.ValidTo:o}");
        var principal = handler.ValidateToken(raw, new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = "fletflow.tests",
            ValidateAudience = true,
            ValidAudience = "fletflow.tests",
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("test-signing-key-with-at-least-32-bytes-long")),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
            NameClaimType = ClaimTypes.NameIdentifier
        }, out _);
        Assert.Equal(userId.ToString(), principal.FindFirst(ClaimTypes.NameIdentifier)?.Value);
    }

    [Fact]
    public void AccessTokenLifetimePolicy_RejectsPreviouslyIssuedLongLivedTokens()
    {
        var service = new JwtTokenService(Options.Create(new JwtSettings
        {
            Key = "test-signing-key-with-at-least-32-bytes-long",
            Issuer = "fletflow.tests",
            Audience = "fletflow.tests",
            ExpireMinutes = 360
        }));
        var raw = service.GenerateToken(
            Guid.NewGuid(), "driver", "driver@example.test", "Driver", Guid.NewGuid());
        var token = new JwtSecurityTokenHandler().ReadJwtToken(raw);

        Assert.False(JwtAccessTokenLifetimePolicy.IsAllowed(token));
    }

    [Fact]
    public void AccessTokenLifetimePolicy_RejectsLegacyTokensWithoutNotBefore()
    {
        var token = new JwtSecurityToken(
            issuer: "fletflow.tests",
            audience: "fletflow.tests",
            claims: [],
            expires: DateTime.UtcNow.AddHours(6));

        Assert.False(JwtAccessTokenLifetimePolicy.IsAllowed(token));
    }
}
