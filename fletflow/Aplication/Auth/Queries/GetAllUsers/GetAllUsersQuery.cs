using fletflow.Application.DTOs.Users;
using fletflow.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace fletflow.Application.Users.Queries
{
    public class GetAllUsersQuery
    {
        private readonly AppDbContext _context;

        public GetAllUsersQuery(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<UserDto>> Execute()
        {
            var users = await _context.Users
                .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                .Select(u => new UserDto
                {
                    Id = u.Id,
                    Username = u.Username,
                    Email = u.Email,
                    Roles = u.UserRoles.Select(ur => ur.Role.Name).ToList(),
                    IsActive = u.IsActive,
                    CreatedAt = u.CreatedAt
                })
                .ToListAsync();

            return users;
        }
    }
}
