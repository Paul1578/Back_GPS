using fletflow.Domain.Auth.Entities;
using fletflow.Domain.Auth.Repositories;
using fletflow.Infrastructure.Persistence.Context;
using fletflow.Infrastructure.Persistence.Mappings;
using fletflow.Infrastructure.Security.Hashing;
using Microsoft.EntityFrameworkCore;

namespace fletflow.Infrastructure.Persistence.Repositories
{
    public class PasswordResetTokenRepository : IPasswordResetTokenRepository
    {
        private readonly AppDbContext _context;
        public PasswordResetTokenRepository(AppDbContext ctx) => _context = ctx;

        public async Task AddAsync(PasswordResetToken token)
        {
            await _context.PasswordResetTokens.AddAsync(PasswordResetTokenMapper.ToEntity(token));
        }

        public async Task<PasswordResetToken?> GetByRawTokenAsync(string rawToken)
        {
            var normalized = Uri.UnescapeDataString(rawToken.Trim()).Replace(" ", "+");
            var hash = TokenHashing.Sha256(normalized);
            var entity = await _context.PasswordResetTokens
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.TokenHash == hash);
            if (entity is null)
            {
                Console.WriteLine($"[ResetPasswordRepo] Token no encontrado. normalized={normalized} hash={hash}");
            }
            return entity is null ? null : PasswordResetTokenMapper.ToDomain(entity);
        }

        public async Task<PasswordResetToken?> GetByTokenHashAsync(string tokenHash)
        {
            var entity = await _context.PasswordResetTokens
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.TokenHash == tokenHash);
            if (entity is null)
            {
                Console.WriteLine($"[ResetPasswordRepo] Token no encontrado por hash={tokenHash}");
            }
            return entity is null ? null : PasswordResetTokenMapper.ToDomain(entity);
        }

        public async Task MarkUsedAsync(Guid id)
        {
            var entity = await _context.PasswordResetTokens.FirstOrDefaultAsync(x => x.Id == id);
            if (entity is null) return;
            entity.UsedAt = DateTime.UtcNow;
        }

        public async Task InvalidateAllForUserAsync(Guid userId)
        {
            var list = await _context.PasswordResetTokens
                .Where(x => x.UserId == userId && x.UsedAt == null && x.ExpiresAt > DateTime.UtcNow)
                .ToListAsync();

            foreach (var e in list) e.UsedAt = DateTime.UtcNow;
        }
    }
}
