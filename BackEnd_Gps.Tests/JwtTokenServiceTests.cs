using System.IdentityModel.Tokens.Jwt;
using fletflow.Infrastructure.Security;
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

        var raw = service.GenerateToken(
            Guid.NewGuid(), "driver", "driver@example.test", "Driver", Guid.NewGuid());

        var token = new JwtSecurityTokenHandler().ReadJwtToken(raw);
        var lifetimeSeconds = (token.ValidTo - beforeGeneration).TotalSeconds;
        Assert.InRange(lifetimeSeconds, 895, 905);
        Assert.True(JwtAccessTokenLifetimePolicy.IsAllowed(token), $"Token ValidFrom={token.ValidFrom:o}; ValidTo={token.ValidTo:o}");
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
