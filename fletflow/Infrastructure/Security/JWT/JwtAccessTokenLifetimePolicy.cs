using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;

namespace fletflow.Infrastructure.Security;

public static class JwtAccessTokenLifetimePolicy
{
    public static readonly TimeSpan MaximumLifetime = TimeSpan.FromMinutes(15);

    public static bool IsAllowed(SecurityToken? token)
    {
        if (token is not JwtSecurityToken jwt)
            return false;

        var lifetime = jwt.ValidTo - jwt.ValidFrom;
        return lifetime > TimeSpan.Zero && lifetime <= MaximumLifetime;
    }
}
