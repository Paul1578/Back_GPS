using fletflow.Domain.Auth.Entities;

namespace fletflow.Domain.Auth.Repositories
{
    public interface IPasswordResetTokenRepository
    {
        Task AddAsync(PasswordResetToken token);
        Task<PasswordResetToken?> GetByRawTokenAsync(string rawToken);
        Task<PasswordResetToken?> GetByTokenHashAsync(string tokenHash);
        Task MarkUsedAsync(Guid id);
        Task InvalidateAllForUserAsync(Guid userId);
    }
}
