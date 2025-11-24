using System;
using fletflow.Domain.Auth.Repositories;
using fletflow.Infrastructure.Persistence.Contracts;
using fletflow.Infrastructure.Security.Hashing;

namespace fletflow.Application.Auth.Commands.ActivateAccount
{
    public class ActivateAccountCommand
    {
        private readonly IPasswordResetTokenRepository _passwordResetTokenRepository;
        private readonly IUserRepository _userRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IPasswordHasher _passwordHasher;

        public ActivateAccountCommand(
            IPasswordResetTokenRepository passwordResetTokenRepository,
            IUserRepository userRepository,
            IUnitOfWork unitOfWork,
            IPasswordHasher passwordHasher)
        {
            _passwordResetTokenRepository = passwordResetTokenRepository;
            _userRepository = userRepository;
            _unitOfWork = unitOfWork;
            _passwordHasher = passwordHasher;
        }

        public async Task ExecuteAsync(string token, string newPassword, string confirmNewPassword)
        {
            if (string.IsNullOrWhiteSpace(token))
                throw new ArgumentException("Token requerido.", nameof(token));
            if (string.IsNullOrWhiteSpace(newPassword))
                throw new ArgumentException("La nueva contrasena es obligatoria.", nameof(newPassword));
            if (newPassword.Length < 6)
                throw new ArgumentException("La nueva contrasena debe tener al menos 6 caracteres.", nameof(newPassword));
            if (newPassword != confirmNewPassword)
                throw new ArgumentException("La confirmacion no coincide con la nueva contrasena.", nameof(confirmNewPassword));

            var rawToken = token.Trim().Replace(" ", "+");
            var tokenHash = TokenHashing.Sha256(rawToken);

            var resetToken = await _passwordResetTokenRepository.GetByTokenHashAsync(tokenHash)
                ?? throw new UnauthorizedAccessException("Token invalido.");

            if (!resetToken.IsActive)
                throw new UnauthorizedAccessException("Token expirado o usado.");

            var user = await _userRepository.GetByIdAsync(resetToken.UserId)
                ?? throw new KeyNotFoundException("Usuario no encontrado.");

            var newPasswordHash = _passwordHasher.Hash(newPassword);
            await _userRepository.UpdatePasswordAndClearFlagAsync(user.Id, newPasswordHash, clearMustChangePassword: true);
            await _passwordResetTokenRepository.MarkUsedAsync(resetToken.Id);

            await _unitOfWork.CommitAsync();
        }
    }
}
