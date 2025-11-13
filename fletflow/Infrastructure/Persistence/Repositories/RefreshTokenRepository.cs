using fletflow.Domain.Auth.Entities;
using fletflow.Domain.Auth.Repositories;
using fletflow.Infrastructure.Persistence.Context;
using fletflow.Infrastructure.Persistence.Mappings;
using fletflow.Infrastructure.Security.Hashing;
using Microsoft.EntityFrameworkCore;

namespace fletflow.Infrastructure.Persistence.Repositories
{
    public class RefreshTokenRepository : IRefreshTokenRepository
    {
        private readonly AppDbContext _context;
        public RefreshTokenRepository(AppDbContext ctx) => _context = ctx;

        public async Task AddAsync(RefreshToken token)
        {
            await _context.RefreshTokens.AddAsync(RefreshTokenMapper.ToEntity(token));
        }

        public async Task<RefreshToken?> GetByTokenAsync(string rawToken)
        {
            var hash = TokenHashing.Sha256(rawToken);
            var entity = await _context.RefreshTokens
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.TokenHash == hash);

            return entity is null ? null : RefreshTokenMapper.ToDomain(entity);
        }

        public async Task RevokeAsync(Guid id, string? replacedByTokenHash = null)
        {
            var entity = await _context.RefreshTokens.FirstOrDefaultAsync(x => x.Id == id);
            if (entity is null) return;
            entity.RevokedAt = DateTime.UtcNow;
            entity.ReplacedByTokenHash = replacedByTokenHash;
        }

        public async Task RemoveExpiredAsync()
        {
            var now = DateTime.UtcNow;
            var expired = await _context.RefreshTokens
                .Where(x => x.ExpiresAt <= now && x.RevokedAt == null)
                .ToListAsync();
            if (expired.Count > 0)
            {
                _context.RefreshTokens.RemoveRange(expired);
            }
        }
        public async Task RevokeAllForUserAsync(Guid userId) 
        {
            var actives = await _context.RefreshTokens
                .Where(x => x.UserId == userId && x.RevokedAt == null)
                .ToListAsync();

            var now = DateTime.UtcNow;
            foreach (var rt in actives)
            {
                rt.RevokedAt = now;
                rt.ReplacedByTokenHash = null;
            }
        }
    }
}
