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
        }
    }
}
