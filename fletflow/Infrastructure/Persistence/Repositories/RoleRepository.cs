using fletflow.Domain.Auth.Entities;
using fletflow.Domain.Auth.Repositories;
using fletflow.Infrastructure.Persistence.Context;
using fletflow.Infrastructure.Persistence.Mappings; 
using Microsoft.EntityFrameworkCore;

namespace fletflow.Infrastructure.Persistence.Repositories
{
    public class RoleRepository : IRoleRepository
    {
        private readonly AppDbContext _context;
        public RoleRepository(AppDbContext context) => _context = context;

        public async Task<Role?> GetByNameAsync(string name)
        {
            var entity = await _context.Roles
                .FirstOrDefaultAsync(r => r.Name.ToLower() == name.Trim().ToLower());

            return entity?.ToDomain();
        }

        public async Task AddAsync(Role role)
        {
            await _context.Roles.AddAsync(role.ToEntity());
        }public async Task<bool> DeleteByNameAsync(string name)
        {
            var entity = await _context.Roles.FirstOrDefaultAsync(r => r.Name.ToLower() == name.Trim().ToLower());
            if (entity is null) return false;

            // protección simple: impide borrar si tiene usuarios
            bool inUse = await _context.UserRoles.AnyAsync(ur => ur.RoleId == entity.Id);
            if (inUse) throw new InvalidOperationException($"No se puede eliminar el rol '{name}' porque tiene usuarios asignados.");

            _context.Roles.Remove(entity);
            return true;
        }

        public async Task<IReadOnlyList<Role>> GetAllAsync()
        {
            var entities = await _context.Roles.AsNoTracking().ToListAsync();
            return entities.Select(e => e.ToDomain()).ToList();
        }
    }
}
