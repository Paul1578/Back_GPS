using System.Security.Cryptography;

namespace fletflow.Api.Security;

public static class TemporaryPasswordGenerator
{
    public static string Generate()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}
