using System.Security.Cryptography;

namespace fletflow.Infrastructure.Security
{
    public static class RefreshTokenFactory
    {
        public static string Create(int bytesLength = 32)
        {
            var bytes = RandomNumberGenerator.GetBytes(bytesLength);
            return Convert.ToBase64String(bytes); 
        }
    }
}
