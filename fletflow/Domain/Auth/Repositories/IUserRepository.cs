using System;
using System.Threading.Tasks;
using fletflow.Domain.Auth.Entities;

namespace fletflow.Domain.Auth.Repositories
{
    public interface IUserRepository
    {
        Task<User?> GetByEmailAsync(string email);
        Task<User?> GetByIdAsync(Guid id);
        Task AddAsync(User user);
        Task SaveChangesAsync();
    }
}
