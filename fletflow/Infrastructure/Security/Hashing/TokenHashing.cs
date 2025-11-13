using System.Security.Cryptography;
using System.Text;

namespace fletflow.Infrastructure.Security.Hashing
{
    public static class TokenHashing
    {
        public static string Sha256(string raw)
        {
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(raw));
            return Convert.ToHexString(bytes); // en .NET 5+: AABBCC...
        }
    }
}
