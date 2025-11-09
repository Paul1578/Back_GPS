using fletflow.Domain.Auth.Entities;
using fletflow.Domain.Auth.Repositories;
using fletflow.Infrastructure.Persistence.Context;
using fletflow.Infrastructure.Persistence.Entities;
using fletflow.Infrastructure.Persistence.Mappings; // AuthMapper
using Microsoft.EntityFrameworkCore;

namespace fletflow.Infrastructure.Persistence.Repositories
{
    public class UserRepository : IUserRepository
    {
        private readonly AppDbContext _context;

        public UserRepository(AppDbContext context) => _context = context;

        public async Task<User?> GetByEmailAsync(string email)
        {
            var entity = await _context.Users
                .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                .FirstOrDefaultAsync(u => u.Email == email);

            return entity is null ? null : AuthMapper.ToDomain(entity); // 👈 calificado
        }

        public async Task<User?> GetByIdAsync(Guid id)
        {
            var entity = await _context.Users
                .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                .FirstOrDefaultAsync(u => u.Id == id);

            return entity is null ? null : AuthMapper.ToDomain(entity); // 👈 calificado
        }

        public async Task AddAsync(User user)
        {
            var entity = AuthMapper.ToEntity(user); // 👈 calificado
            await _context.Users.AddAsync(entity);
        }

        public async Task SaveChangesAsync() => await _context.SaveChangesAsync();
    }
}
