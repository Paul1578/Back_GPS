using fletflow.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace fletflow.Application.Users.Commands
{
    public class UpdateUserCommand
    {
        private readonly AppDbContext _context;

        public UpdateUserCommand(AppDbContext context)
        {
            _context = context;
        }

        public async Task Execute(Guid userId, bool isActive)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null)
                throw new Exception("Usuario no encontrado.");

            user.IsActive = isActive;
            await _context.SaveChangesAsync();
        }
    }
}
