using fletflow.Domain.Auth.Entities;
using fletflow.Infrastructure.Persistence.Entities;

namespace fletflow.Infrastructure.Persistence.Mappings
{
    public static class RefreshTokenMapper
    {
        public static RefreshTokenEntity ToEntity(RefreshToken d) => new()
        {
            Id = d.Id,
            UserId = d.UserId,
            TokenHash = d.TokenHash,
            ExpiresAt = d.ExpiresAt,
            CreatedAt = d.CreatedAt,
            RevokedAt = d.RevokedAt,
            ReplacedByTokenHash = d.ReplacedByTokenHash
        };

        public static RefreshToken ToDomain(RefreshTokenEntity e) => new()
        {
            Id = e.Id,
            UserId = e.UserId,
            TokenHash = e.TokenHash,
            ExpiresAt = e.ExpiresAt,
            CreatedAt = e.CreatedAt,
            RevokedAt = e.RevokedAt,
            ReplacedByTokenHash = e.ReplacedByTokenHash
        };
    }
}
