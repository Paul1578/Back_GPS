using System;
using fletflow.Domain.Auth.Repositories;
using fletflow.Infrastructure.Persistence.Contracts;
using fletflow.Infrastructure.Security.Hashing;

namespace fletflow.Application.Auth.Commands.ResetPassword
{
    public class ResetPasswordCommand
    {
        private readonly IPasswordResetTokenRepository _passwordResetTokenRepository;
        private readonly IUserRepository _userRepository;
        private readonly IRefreshTokenRepository _refreshTokenRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IPasswordHasher _passwordHasher;

        public ResetPasswordCommand(
            IPasswordResetTokenRepository passwordResetTokenRepository,
            IUserRepository userRepository,
            IRefreshTokenRepository refreshTokenRepository,
            IUnitOfWork unitOfWork,
            IPasswordHasher passwordHasher)
        {
            _passwordResetTokenRepository = passwordResetTokenRepository;
            _userRepository = userRepository;
            _refreshTokenRepository = refreshTokenRepository;
            _unitOfWork = unitOfWork;
            _passwordHasher = passwordHasher;
        }

        public async Task ExecuteAsync(string requestToken, string newPassword, string? confirmNewPassword = null)
        {
            if (string.IsNullOrWhiteSpace(requestToken))
                throw new ArgumentException("Token requerido.", nameof(requestToken));

            if (string.IsNullOrWhiteSpace(newPassword))
                throw new ArgumentException("La nueva contrasena es obligatoria.", nameof(newPassword));

            if (newPassword.Length < 12)
                throw new ArgumentException("La nueva contraseña debe tener al menos 12 caracteres.", nameof(newPassword));

            if (confirmNewPassword != null && newPassword != confirmNewPassword)
                throw new ArgumentException("La confirmacion no coincide con la nueva contrasena.", nameof(confirmNewPassword));

            // Token plano (no requiere decodificar). Reemplazamos espacios defensivamente.
            var rawToken = (requestToken ?? string.Empty).Trim().Replace(" ", "+");
            var tokenHash = TokenHashing.Sha256(rawToken);

            var initialToken = await _passwordResetTokenRepository.GetByTokenHashAsync(tokenHash)
                ?? throw new UnauthorizedAccessException("Token invalido.");

            if (!initialToken.IsActive)
                throw new UnauthorizedAccessException("Token expirado o usado.");

            await _unitOfWork.LockUserSessionAsync(initialToken.UserId);
            try
            {
                var resetToken = await _passwordResetTokenRepository.GetByTokenHashAsync(tokenHash)
                    ?? throw new UnauthorizedAccessException("Token invalido.");
                if (!resetToken.IsActive || !await _passwordResetTokenRepository.MarkUsedAsync(resetToken.Id))
                    throw new UnauthorizedAccessException("Token expirado o usado.");

                var user = await _userRepository.GetByIdAsync(resetToken.UserId)
                    ?? throw new KeyNotFoundException("Usuario no encontrado.");

                var newPasswordHash = _passwordHasher.Hash(newPassword);
                await _userRepository.UpdatePasswordHashAsync(user.Id, newPasswordHash);
                await _refreshTokenRepository.RevokeAllForUserAsync(user.Id);
                await _unitOfWork.CommitAsync();
            }
            catch
            {
                _unitOfWork.Rollback();
                throw;
            }
        }
    }
}
