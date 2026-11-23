using Microsoft.IdentityModel.Tokens;

namespace fletflow.Infrastructure.Security;

public static class JwtAccessTokenLifetimePolicy
{
    public static readonly TimeSpan MaximumLifetime = TimeSpan.FromMinutes(15);

    public static bool IsAllowed(SecurityToken? token)
    {
        if (token is null)
            return false;

        var lifetime = token.ValidTo - token.ValidFrom;
        return lifetime > TimeSpan.Zero && lifetime <= MaximumLifetime;
    }
}
