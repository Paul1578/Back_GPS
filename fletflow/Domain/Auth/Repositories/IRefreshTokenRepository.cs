using fletflow.Domain.Auth.Entities;

namespace fletflow.Domain.Auth.Repositories
{
    public interface IRefreshTokenRepository
    {
        Task AddAsync(RefreshToken token);
        Task<RefreshToken?> GetByTokenAsync(string rawToken); 
        Task RevokeAsync(Guid id, string? replacedByTokenHash = null);
        Task<bool> TryRotateAsync(Guid currentTokenId, RefreshToken replacement);
        Task RemoveExpiredAsync();
        Task RevokeAllForUserAsync(Guid userId);
    }
}
