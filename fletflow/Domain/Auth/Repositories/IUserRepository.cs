using fletflow.Domain.Auth.Entities;

namespace fletflow.Domain.Auth.Repositories
{
    public interface IUserRepository
    {
        Task<User?> GetByEmailAsync(string email);
        Task<User?> GetByIdAsync(Guid id);
        Task AddAsync(User user);
        Task SaveChangesAsync();

        Task<bool> HasRoleAsync(Guid userId, Guid roleId);
        Task AssignRoleAsync(Guid userId, Guid roleId);
        Task RemoveRoleAsync(Guid userId, Guid roleId);

        Task UpdatePasswordHashAsync(Guid userId, string newPasswordHash);
        Task UpdatePasswordAndClearFlagAsync(Guid userId, string newPasswordHash, bool clearMustChangePassword);
        Task<List<User>> GetByOwnerAsync(Guid ownerUserId);
    }
}
