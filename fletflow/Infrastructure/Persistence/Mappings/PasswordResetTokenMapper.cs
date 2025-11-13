using fletflow.Domain.Auth.Entities;
using fletflow.Infrastructure.Persistence.Entities;

namespace fletflow.Infrastructure.Persistence.Mappings
{
    public static class PasswordResetTokenMapper
    {
        public static PasswordResetTokenEntity ToEntity(PasswordResetToken d) => new()
        {
            Id = d.Id,
            UserId = d.UserId,
            TokenHash = d.TokenHash,
            ExpiresAt = d.ExpiresAt,
            CreatedAt = d.CreatedAt,
            UsedAt = d.UsedAt
        };

        public static PasswordResetToken ToDomain(PasswordResetTokenEntity e) => new()
        {
            Id = e.Id,
            UserId = e.UserId,
            TokenHash = e.TokenHash,
            ExpiresAt = e.ExpiresAt,
            CreatedAt = e.CreatedAt,
            UsedAt = e.UsedAt
        };
    }
}
