using fletflow.Infrastructure.Persistence.Contracts;
using fletflow.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace fletflow.Application.Users.Commands
{
    public class UpdateUserCommand
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly AppDbContext _context;

        public UpdateUserCommand(IUnitOfWork unitOfWork, AppDbContext context)
        {
            _unitOfWork = unitOfWork;
            _context = context;
        }

        public async Task Execute(Guid userId, bool isActive)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null)
                throw new KeyNotFoundException("Usuario no encontrado.");

            user.IsActive = isActive;
            if (!isActive)
                await _unitOfWork.RefreshTokens.RevokeAllForUserAsync(userId);
            await _unitOfWork.CommitAsync();
        }
    }
}
