using System;
using System.Security.Cryptography;
using fletflow.Domain.Auth.Entities;
using fletflow.Infrastructure.Security.Hashing;

namespace fletflow.Infrastructure.Services
{
    /// <summary>
    /// Genera tokens cortos (12 chars) en Base64 URL-friendly y su hash persistible.
    /// </summary>
    public class PasswordResetTokenFactory
    {
        public (PasswordResetToken token, string plainToken) Create(Guid userId, int expirationMinutes)
        {
            // 9 bytes generan 12 caracteres Base64
            var bytes = RandomNumberGenerator.GetBytes(9);
            var plainToken = Convert.ToBase64String(bytes)
                .Replace("/", "_")
                .Replace("+", "-")
                .Replace("=", "");

            var tokenHash = TokenHashing.Sha256(plainToken);
            var expiresAt = DateTime.UtcNow.AddMinutes(expirationMinutes);

            var token = new PasswordResetToken
            {
                UserId = userId,
                TokenHash = tokenHash,
                ExpiresAt = expiresAt,
                CreatedAt = DateTime.UtcNow
            };

            return (token, plainToken);
        }
    }
}
