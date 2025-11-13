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
            var user = await _users.GetByIdAsync(userId)
                ?? throw new KeyNotFoundException("Usuario no encontrado.");

            if (!BCrypt.Net.BCrypt.Verify(currentPassword, user.PasswordHash))
                throw new UnauthorizedAccessException("La contraseña actual es incorrecta.");

            var newHash = BCrypt.Net.BCrypt.HashPassword(newPassword);

            // actualización sobre la entidad EF
            await _users.UpdatePasswordHashAsync(userId, newHash);

            await _uow.RefreshTokens.RevokeAllForUserAsync(user.Id);

            // commit con UnitOfWork
            await _uow.CommitAsync();
        }
    }
}
