using fletflow.Domain.Auth.Entities;
using fletflow.Domain.Auth.Repositories;
using fletflow.Infrastructure.Persistence.Context;
using fletflow.Infrastructure.Persistence.Mappings;
using fletflow.Infrastructure.Security.Hashing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System.Data;

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

        public async Task<bool> TryRotateAsync(Guid currentTokenId, RefreshToken replacement)
        {
            var now = DateTime.UtcNow;
            if (!_context.Database.IsRelational())
            {
                var current = await _context.RefreshTokens.FirstOrDefaultAsync(token =>
                    token.Id == currentTokenId && token.RevokedAt == null && token.ExpiresAt > now);
                if (current is null) return false;
                current.RevokedAt = now;
                current.ReplacedByTokenHash = replacement.TokenHash;
                await _context.RefreshTokens.AddAsync(RefreshTokenMapper.ToEntity(replacement));
                await _context.SaveChangesAsync();
                return true;
            }

            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
            if (_context.Database.ProviderName!.Contains("MySql", StringComparison.OrdinalIgnoreCase) &&
                !await LockUserRowAsync(replacement.UserId, transaction))
            {
                await transaction.RollbackAsync();
                return false;
            }

            var affected = await _context.RefreshTokens
                .Where(token => token.Id == currentTokenId && token.RevokedAt == null && token.ExpiresAt > now)
                .ExecuteUpdateAsync(updates => updates
                    .SetProperty(token => token.RevokedAt, now)
                    .SetProperty(token => token.ReplacedByTokenHash, replacement.TokenHash));
            if (affected != 1)
            {
                await transaction.RollbackAsync();
                return false;
            }

            await _context.RefreshTokens.AddAsync(RefreshTokenMapper.ToEntity(replacement));
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return true;
        }

        private async Task<bool> LockUserRowAsync(Guid userId, IDbContextTransaction transaction)
        {
            var connection = _context.Database.GetDbConnection();
            await using var command = connection.CreateCommand();
            command.Transaction = transaction.GetDbTransaction();
            command.CommandText = "SELECT `Id` FROM `Users` WHERE `Id` = @id FOR UPDATE";
            var idParameter = command.CreateParameter();
            idParameter.ParameterName = "@id";
            idParameter.DbType = DbType.Guid;
            idParameter.Value = userId;
            command.Parameters.Add(idParameter);
            return await command.ExecuteScalarAsync() is not null;
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
