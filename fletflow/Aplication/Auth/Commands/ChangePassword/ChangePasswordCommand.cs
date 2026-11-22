using fletflow.Domain.Auth.Repositories;
using fletflow.Infrastructure.Persistence.Contracts;

namespace fletflow.Application.Auth.Commands
{
    public class ChangePasswordCommand
    {
        private readonly IUnitOfWork _uow;
        private readonly IUserRepository _users;

        public ChangePasswordCommand(IUnitOfWork uow, IUserRepository users)
        {
            _uow = uow;
            _users = users;
        }

        public async Task Execute(Guid userId, string currentPassword, string newPassword)
        {
            await _uow.LockUserSessionAsync(userId);
            try
            {
                var user = await _users.GetByIdAsync(userId)
                    ?? throw new KeyNotFoundException("User not found.");

                if (!BCrypt.Net.BCrypt.Verify(currentPassword, user.PasswordHash))
                    throw new UnauthorizedAccessException("Current password is incorrect.");

                var newHash = BCrypt.Net.BCrypt.HashPassword(newPassword);

                await _users.UpdatePasswordHashAsync(userId, newHash);
                await _uow.RefreshTokens.RevokeAllForUserAsync(user.Id);
                await _uow.CommitAsync();
            }
            catch
            {
                _uow.Rollback();
                throw;
            }
        }
    }
}
