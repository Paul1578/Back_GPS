using fletflow.Domain.Auth.Entities;

namespace fletflow.Domain.Auth.Repositories
{
    public interface IRoleRepository
    {
        Task<Role?> GetByNameAsync(string name);
        Task AddAsync(Role role);
    
        Task<bool> DeleteByNameAsync(string name);
        Task<IReadOnlyList<Role>> GetAllAsync();
    
    }


}
