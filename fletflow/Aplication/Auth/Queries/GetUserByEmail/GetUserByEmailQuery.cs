using fletflow.Domain.Auth.Entities;
using fletflow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore; 

namespace fletflow.Aplication.Auth.Queries
{
    public class GetUserByEmailQuery
    {
        private readonly AppDbContext _context;

        public GetUserByEmailQuery(AppDbContext context)
        {
            _context = context;
        }

        public async Task<User?> Execute(string email)
        {
            return await _context.Users
                .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                .FirstOrDefaultAsync(u => u.Email == email);
        }
    }
}