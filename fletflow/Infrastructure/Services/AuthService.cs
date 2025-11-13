using fletflow.Application.Auth.Commands;
using fletflow.Application.DTOs.Auth;
using fletflow.Domain.Auth.Entities;
using fletflow.Infrastructure.Persistence.Contracts;  // IUnitOfWork
using fletflow.Infrastructure.Security;
using fletflow.Infrastructure.Security.Hashing;

namespace fletflow.Infrastructure.Services
{
    public class AuthService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly JwtTokenService _jwtService;

        public AuthService(IUnitOfWork unitOfWork, JwtTokenService jwtService)
        {
            _unitOfWork = unitOfWork;
            _jwtService = jwtService;
        }

        // Access + Refresh
        public async Task<AuthResponseDto> RegisterAsync(string username, string email, string password, string roleName = "User")
        {
            var cmd = new RegisterUserCommand(_unitOfWork);
            var user = await cmd.Execute(username, email, password, roleName);
            return await IssueTokensAsync(user);
        }

        // Access + Refresh
        public async Task<AuthResponseDto> LoginAsync(string email, string password)
        {
            var cmd = new LoginUserCommand(_unitOfWork);
            var user = await cmd.Execute(email, password);
            return await IssueTokensAsync(user);
        }

        public async Task<AuthResponseDto> RefreshAsync(string refreshTokenRaw)
        {
            var rt = await _unitOfWork.RefreshTokens.GetByTokenAsync(refreshTokenRaw)
                ?? throw new UnauthorizedAccessException("RT inválido.");

            if (!rt.IsActive)
                throw new UnauthorizedAccessException("RT expirado o revocado.");

            var user = await _unitOfWork.Users.GetByIdAsync(rt.UserId)
                ?? throw new KeyNotFoundException("Usuario no encontrado.");

            // rotación de refresh
            var newRaw  = RefreshTokenFactory.Create();
            var newHash = TokenHashing.Sha256(newRaw);

            var newRt = new RefreshToken
            {
                UserId = user.Id,
                TokenHash = newHash,
                ExpiresAt = DateTime.UtcNow.AddDays(7)
            };

            await _unitOfWork.RefreshTokens.RevokeAsync(rt.Id, replacedByTokenHash: newHash);
            await _unitOfWork.RefreshTokens.AddAsync(newRt);
            await _unitOfWork.CommitAsync();

            return BuildResponse(user, newRaw, newRt.ExpiresAt);
        }

        public async Task LogoutAsync(string refreshTokenRaw)
        {
            var rt = await _unitOfWork.RefreshTokens.GetByTokenAsync(refreshTokenRaw);
            if (rt == null) return; // idempotente

            await _unitOfWork.RefreshTokens.RevokeAsync(rt.Id);
            await _unitOfWork.CommitAsync();
        }

        public async Task LogoutAllAsync(Guid userId)
        {
            await _unitOfWork.RefreshTokens.RevokeAllForUserAsync(userId);
            await _unitOfWork.CommitAsync();
        }

        public async Task<MeResponseDto> GetMeAsync(Guid userId)
        {
            var user = await _unitOfWork.Users.GetByIdAsync(userId)
                ?? throw new KeyNotFoundException("Usuario no encontrado.");

            return new MeResponseDto
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                IsActive = user.IsActive,
                Roles = user.UserRoles.Select(ur => ur.Role.Name.Trim()).ToList()
            };
        }

        public async Task<ForgotPasswordResponseDto> ForgotPasswordAsync(string email, bool returnTokenInResponse = true)
        {
            var user = await _unitOfWork.Users.GetByEmailAsync(email);

            // respuesta ciega
            string? raw = null;
            DateTime? exp = null;

            if (user != null)
            {
                await _unitOfWork.PasswordResetTokens.InvalidateAllForUserAsync(user.Id);

                raw = RefreshTokenFactory.Create(32);
                var hash = TokenHashing.Sha256(raw);

                var prt = new PasswordResetToken
                {
                    UserId = user.Id,
                    TokenHash = hash,
                    ExpiresAt = DateTime.UtcNow.AddMinutes(30)
                };

                await _unitOfWork.PasswordResetTokens.AddAsync(prt);
                await _unitOfWork.CommitAsync();

                exp = prt.ExpiresAt;
                // TODO: enviar 'raw' por correo en producción
            }

            return new ForgotPasswordResponseDto
            {
                DebugToken = returnTokenInResponse ? raw : null,
                ExpiresAt = exp
            };
        }

        public async Task ResetPasswordAsync(string rawToken, string newPassword)
        {
            var prt = await _unitOfWork.PasswordResetTokens.GetByRawTokenAsync(rawToken)
                ?? throw new UnauthorizedAccessException("Token inválido.");

            if (!prt.IsActive)
                throw new UnauthorizedAccessException("Token expirado o usado.");

            var user = await _unitOfWork.Users.GetByIdAsync(prt.UserId)
                ?? throw new KeyNotFoundException("Usuario no encontrado.");

            // ✅ generar hash y PERSISTIRLO mediante el repositorio
            var newHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
            await _unitOfWork.Users.UpdatePasswordHashAsync(user.Id, newHash);

            // marcar token como usado
            await _unitOfWork.PasswordResetTokens.MarkUsedAsync(prt.Id);

            // (opcional) revocar todos los refresh tokens del usuario:
            // await _unitOfWork.RefreshTokens.RevokeAllForUserAsync(user.Id);

            await _unitOfWork.CommitAsync();
        }

        // ---- helpers ----
        private async Task<AuthResponseDto> IssueTokensAsync(User user)
        {
            var role = user.UserRoles.First().Role.Name.Trim();

            var access = _jwtService.GenerateToken(user.Id, user.Username, user.Email, role);

            var raw  = RefreshTokenFactory.Create();
            var hash = TokenHashing.Sha256(raw);

            var rt = new RefreshToken
            {
                UserId = user.Id,
                TokenHash = hash,
                ExpiresAt = DateTime.UtcNow.AddDays(7)
            };

            await _unitOfWork.RefreshTokens.AddAsync(rt);
            await _unitOfWork.CommitAsync();

            return BuildResponse(user, raw, rt.ExpiresAt);
        }

        private AuthResponseDto BuildResponse(User user, string refreshRaw, DateTime rtExp)
        {
            var role = user.UserRoles.First().Role.Name.Trim();
            var access = _jwtService.GenerateToken(user.Id, user.Username, user.Email, role);

            return new AuthResponseDto
            {
                Token = access,
                // si quieres usar JwtSettings.ExpireMinutes, inyecta IOptions<JwtSettings> y calcula aquí
                Expiration = DateTime.UtcNow.AddMinutes(60),
                RefreshToken = refreshRaw,
                RefreshTokenExpiration = rtExp
            };
        }
    }
}
