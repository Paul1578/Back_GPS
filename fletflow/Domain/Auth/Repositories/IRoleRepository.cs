using fletflow.Domain.Auth.Entities;

namespace fletflow.Domain.Auth.Repositories
{
    public interface IRoleRepository
    {
        Task<Role?> GetByNameAsync(string name);
        Task AddAsync(Role role);
    }
}
