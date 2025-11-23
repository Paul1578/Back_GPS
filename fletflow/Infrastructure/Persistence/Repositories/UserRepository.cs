using fletflow.Domain.Auth.Entities;
using fletflow.Domain.Auth.Repositories;
using fletflow.Infrastructure.Persistence.Context;
using fletflow.Infrastructure.Persistence.Entities;
using fletflow.Infrastructure.Persistence.Mappings;
using Microsoft.EntityFrameworkCore;

namespace fletflow.Infrastructure.Persistence.Repositories
{
    public class UserRepository : IUserRepository
    {
        private readonly AppDbContext _context;

        public UserRepository(AppDbContext context) => _context = context;

        public async Task<User?> GetByEmailAsync(string email)
        {
            var normalized = email.Trim().ToLowerInvariant();
            var entity = await _context.Users
                .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
                .FirstOrDefaultAsync(u => u.Email.ToLower() == normalized);

            return entity is null ? null : AuthMapper.ToDomain(entity);
        }

        public async Task<User?> GetByIdAsync(Guid id)
        {
            var entity = await _context.Users
                .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
                .FirstOrDefaultAsync(u => u.Id == id);

            return entity is null ? null : AuthMapper.ToDomain(entity);
        }

        public async Task AddAsync(User user)
        {
            var entity = AuthMapper.ToEntity(user);
            await _context.Users.AddAsync(entity);
        }

        public Task SaveChangesAsync() => _context.SaveChangesAsync();

        public async Task<bool> HasRoleAsync(Guid userId, Guid roleId)
        {
            return await _context.UserRoles.AnyAsync(ur => ur.UserId == userId && ur.RoleId == roleId);
        }

        public async Task AssignRoleAsync(Guid userId, Guid roleId)
        {
            if (await HasRoleAsync(userId, roleId)) return;
            await _context.UserRoles.AddAsync(new UserRoleEntity { UserId = userId, RoleId = roleId });
        }

        public async Task RemoveRoleAsync(Guid userId, Guid roleId)
        {
            var link = await _context.UserRoles.FirstOrDefaultAsync(ur => ur.UserId == userId && ur.RoleId == roleId);
            if (link is not null) _context.UserRoles.Remove(link);
        }

        // ✅ implementación que persiste el hash realmente en DB
        public async Task UpdatePasswordHashAsync(Guid userId, string newPasswordHash)
        {
            var entity = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (entity is null) throw new KeyNotFoundException("Usuario no encontrado.");
            entity.PasswordHash = newPasswordHash;
            _context.Users.Update(entity);
        }
    }
}
