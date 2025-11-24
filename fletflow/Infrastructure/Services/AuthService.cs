using fletflow.Application.Auth.Commands;
using fletflow.Application.DTOs.Auth;
using fletflow.Domain.Auth.Entities;
using fletflow.Infrastructure.Persistence.Contracts;  // IUnitOfWork
using fletflow.Infrastructure.Security;
using fletflow.Infrastructure.Security.Hashing;
using Microsoft.Extensions.Options;

namespace fletflow.Infrastructure.Services
{
    public class AuthService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly JwtTokenService _jwtService;
        private readonly IEmailSender _emailService;
        private readonly EmailSettings _emailSettings;
        private readonly PasswordResetTokenFactory _passwordResetTokenFactory;

        public AuthService(IUnitOfWork unitOfWork, JwtTokenService jwtService, IEmailSender emailService, PasswordResetTokenFactory passwordResetTokenFactory, IOptions<EmailSettings> emailOptions)
        {
            _unitOfWork = unitOfWork;
            _jwtService = jwtService;
            _emailService = emailService;
            _passwordResetTokenFactory = passwordResetTokenFactory;
            _emailSettings = emailOptions.Value;
        }

        // Access + Refresh
        public async Task<AuthResponseDto> RegisterAsync(string username, string email, string password, string roleName = "User")
        {
            var cmd = new RegisterUserCommand(_unitOfWork);
            var user = await cmd.Execute(username, email, password, roleName, mustChangePassword: false);
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
                ?? throw new UnauthorizedAccessException("RT invalido.");

            if (!rt.IsActive)
                throw new UnauthorizedAccessException("RT expirado o revocado.");

            var user = await _unitOfWork.Users.GetByIdAsync(rt.UserId)
                ?? throw new KeyNotFoundException("Usuario no encontrado.");

            // rotacion de refresh
            var newRaw = RefreshTokenFactory.Create();
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

        public async Task<ForgotPasswordResponseDto> ForgotPasswordAsync(string email, bool returnTokenInResponse = false)
        {
            var normalizedEmail = email?.Trim();
            Console.WriteLine($"[ForgotPassword] Solicitud para {normalizedEmail}");
            if (string.IsNullOrWhiteSpace(normalizedEmail))
                return new ForgotPasswordResponseDto();

            var user = await _unitOfWork.Users.GetByEmailAsync(normalizedEmail);

            // respuesta ciega
            string? raw = null;
            DateTime? exp = null;

            if (user != null)
            {
                Console.WriteLine($"[ForgotPassword] Usuario encontrado {user.Email}");
                await _unitOfWork.PasswordResetTokens.InvalidateAllForUserAsync(user.Id);

                var (token, plainToken) = _passwordResetTokenFactory.Create(user.Id, _emailSettings.ResetTokenMinutes);
                raw = plainToken;

                await _unitOfWork.PasswordResetTokens.AddAsync(token);
                var saved = await _unitOfWork.CommitAsync();
                Console.WriteLine($"[ForgotPassword] Token guardado? filas={saved} tokenId={token.Id}");

                exp = token.ExpiresAt;
                var encoded = Uri.EscapeDataString(plainToken);
                var link = $"{_emailSettings.ResetPasswordUrl}?token={encoded}";
                Console.WriteLine($"[ForgotPassword] Enviando correo a {user.Email} link={link} plain={plainToken}");
                await _emailService.SendPasswordResetEmailAsync(user.Email, link, plainToken);
            }
            else
            {
                Console.WriteLine($"[ForgotPassword] Email no encontrado {normalizedEmail}");
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
                ?? throw new UnauthorizedAccessException("Token invalido.");

            if (!prt.IsActive)
                throw new UnauthorizedAccessException("Token expirado o usado.");

            var user = await _unitOfWork.Users.GetByIdAsync(prt.UserId)
                ?? throw new KeyNotFoundException("Usuario no encontrado.");

            // generar hash y persistirlo mediante el repositorio
            var newHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
            await _unitOfWork.Users.UpdatePasswordHashAsync(user.Id, newHash);

            // marcar token como usado
            await _unitOfWork.PasswordResetTokens.MarkUsedAsync(prt.Id);

            // opcional: revocar todos los refresh tokens del usuario
            // await _unitOfWork.RefreshTokens.RevokeAllForUserAsync(user.Id);

            await _unitOfWork.CommitAsync();
        }

        // ---- helpers ----
        private async Task<AuthResponseDto> IssueTokensAsync(User user)
        {
            var role = user.UserRoles.First().Role.Name.Trim();

            var access = _jwtService.GenerateToken(user.Id, user.Username, user.Email, role);

            var raw = RefreshTokenFactory.Create();
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
                Expiration = DateTime.UtcNow.AddMinutes(60),
                RefreshToken = refreshRaw,
                RefreshTokenExpiration = rtExp
            };
        }
    }
}
