using fletflow.Application.Auth.Commands;
using fletflow.Application.DTOs.Auth;
using fletflow.Domain.Auth.Entities;
using fletflow.Domain.Fleet.Entities;
using fletflow.Infrastructure.Persistence.Contracts;  // IUnitOfWork
using fletflow.Infrastructure.Security;
using fletflow.Infrastructure.Security.Hashing;
using fletflow.Domain.Fleet.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace fletflow.Infrastructure.Services
{
    public class AuthService : IUserRegistrationService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly JwtTokenService _jwtService;
        private readonly IEmailDeliveryQueue _emailDeliveryQueue;
        private readonly EmailSettings _emailSettings;
        private readonly PasswordResetTokenFactory _passwordResetTokenFactory;
        private readonly ILogger<AuthService> _logger;

        public AuthService(IUnitOfWork unitOfWork, JwtTokenService jwtService, IEmailDeliveryQueue emailDeliveryQueue, PasswordResetTokenFactory passwordResetTokenFactory, IOptions<EmailSettings> emailOptions, ILogger<AuthService> logger)
        {
            _unitOfWork = unitOfWork;
            _jwtService = jwtService;
            _emailDeliveryQueue = emailDeliveryQueue;
            _passwordResetTokenFactory = passwordResetTokenFactory;
            _emailSettings = emailOptions.Value;
            _logger = logger;
        }

        // Access + Refresh
        public async Task RegisterAsync(string username, string email, string password, string roleName = "User")
        {
            email = email.Trim();
            var cmd = new RegisterUserCommand(_unitOfWork);
            var user = await cmd.Execute(
                username,
                email,
                password,
                roleName,
                ownerUserId: null,
                mustChangePassword: false,
                saveChanges: false);

            // Auto-crear Driver si el rol es Driver
            if (roleName.Trim().Equals("Driver", StringComparison.OrdinalIgnoreCase))
            {
                var existingDriver = await _unitOfWork.Drivers.GetByUserIdAsync(user.Id);
                if (existingDriver is null)
                {
                    // Generar datos mínimos para driver
                    var first = string.IsNullOrWhiteSpace(user.Username) ? "Driver" : user.Username;
                    var last = "Driver";
                    var doc = string.IsNullOrWhiteSpace(user.Email)
                        ? user.Id.ToString("N")[..Math.Min(20, user.Id.ToString("N").Length)]
                        : user.Email.Length > 20 ? user.Email[..20] : user.Email;
                    var phone = "N/A";

                    var driver = Driver.Create(first, last, doc, phone, user.Id);
                    await _unitOfWork.Drivers.AddAsync(driver);
                }
            }

            try
            {
                await _unitOfWork.CommitAsync();
            }
            catch (DbUpdateException)
            {
                // El índice único puede ganar una carrera luego del pre-check.
                if (await _unitOfWork.Users.GetByEmailAsync(email) is not null)
                    throw new BusinessConflictException("El correo ya fue registrado.");
                throw;
            }
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
            if (!user.IsActive)
                throw new UnauthorizedAccessException("La cuenta está desactivada.");

            // rotacion de refresh
            var newRaw = RefreshTokenFactory.Create();
            var newHash = TokenHashing.Sha256(newRaw);

            var newRt = new RefreshToken
            {
                UserId = user.Id,
                TokenHash = newHash,
                ExpiresAt = DateTime.UtcNow.AddDays(7)
            };

            if (!await _unitOfWork.RefreshTokens.TryRotateAsync(rt.Id, newRt))
                throw new UnauthorizedAccessException("RT expirado o revocado.");

            return BuildResponse(user, newRaw, newRt.ExpiresAt);
        }

        public async Task LogoutAsync(string refreshTokenRaw, Guid authenticatedUserId)
        {
            var rt = await _unitOfWork.RefreshTokens.GetByTokenAsync(refreshTokenRaw);
            if (rt == null || rt.UserId != authenticatedUserId) return; // idempotente y limitado a la sesión del usuario

            await _unitOfWork.RefreshTokens.RevokeAsync(rt.Id);
            await _unitOfWork.CommitAsync();
        }

        public async Task LogoutAllAsync(Guid userId)
        {
            await _unitOfWork.LockUserSessionAsync(userId);
            await _unitOfWork.Users.IncrementTokenVersionAsync(userId);
            await _unitOfWork.RefreshTokens.RevokeAllForUserAsync(userId);
            await _unitOfWork.CommitAsync();
        }

        public async Task<MeResponseDto> GetMeAsync(Guid userId)
        {
            var user = await _unitOfWork.Users.GetByIdAsync(userId)
                ?? throw new KeyNotFoundException("Usuario no encontrado.");

            var driver = await _unitOfWork.Drivers.GetByUserIdAsync(userId);

            return new MeResponseDto
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                IsActive = user.IsActive,
                IsRootAccount = user.OwnerUserId is null || user.OwnerUserId == user.Id,
                Roles = user.UserRoles.Select(ur => ur.Role.Name.Trim()).ToList(),
                DriverId = driver?.Id
            };
        }

        public async Task<ForgotPasswordResponseDto> ForgotPasswordAsync(string email, bool returnTokenInResponse = false)
        {
            var startedAt = Stopwatch.GetTimestamp();
            var normalizedEmail = email?.Trim();
            if (string.IsNullOrWhiteSpace(normalizedEmail))
            {
                await WaitForRecoveryResponseFloorAsync(startedAt);
                return new ForgotPasswordResponseDto();
            }

            var user = await _unitOfWork.Users.GetByEmailAsync(normalizedEmail);

            // Mantener exactamente la misma respuesta para cuentas existentes e inexistentes.
            string? raw = null;

            if (user != null)
            {
                // Serialize password-recovery requests for this account so concurrent
                // requests cannot leave two newly issued reset tokens active.
                await _unitOfWork.LockUserSessionAsync(user.Id);
                await _unitOfWork.PasswordResetTokens.InvalidateAllForUserAsync(user.Id);

                var (token, plainToken) = _passwordResetTokenFactory.Create(user.Id, _emailSettings.ResetTokenMinutes);
                raw = plainToken;

                var encoded = Uri.EscapeDataString(plainToken);
                var link = $"{_emailSettings.ResetPasswordUrl}?token={encoded}";
                await _unitOfWork.PasswordResetTokens.AddAsync(token);
                await _emailDeliveryQueue.QueuePasswordResetAsync(user.Id, user.Email, link, plainToken);
                await _unitOfWork.CommitAsync();
            }

            // SMTP latency must not expose account existence. Keep both response paths
            // above the same minimum while delivery proceeds outside this request.
            await WaitForRecoveryResponseFloorAsync(startedAt);

            return new ForgotPasswordResponseDto
            {
                DebugToken = returnTokenInResponse ? raw : null,
                ExpiresAt = null
            };
        }

        private static async Task WaitForRecoveryResponseFloorAsync(long startedAt)
        {
            var floor = TimeSpan.FromMilliseconds(250);
            var elapsed = Stopwatch.GetElapsedTime(startedAt);
            if (elapsed < floor)
                await Task.Delay(floor - elapsed);
        }

        public async Task ResetPasswordAsync(string rawToken, string newPassword)
        {
            var prt = await _unitOfWork.PasswordResetTokens.GetByRawTokenAsync(rawToken)
                ?? throw new UnauthorizedAccessException("Token invalido.");

            if (!prt.IsActive)
                throw new UnauthorizedAccessException("Token expirado o usado.");

            await _unitOfWork.LockUserSessionAsync(prt.UserId);
            try
            {
                prt = await _unitOfWork.PasswordResetTokens.GetByRawTokenAsync(rawToken)
                    ?? throw new UnauthorizedAccessException("Token invalido.");
                if (!prt.IsActive || !await _unitOfWork.PasswordResetTokens.MarkUsedAsync(prt.Id))
                    throw new UnauthorizedAccessException("Token expirado o usado.");

                var user = await _unitOfWork.Users.GetByIdAsync(prt.UserId)
                    ?? throw new KeyNotFoundException("Usuario no encontrado.");
                var newHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
                await _unitOfWork.Users.UpdatePasswordHashAsync(user.Id, newHash);
                await _unitOfWork.RefreshTokens.RevokeAllForUserAsync(user.Id);
                await _unitOfWork.CommitAsync();
            }
            catch
            {
                _unitOfWork.Rollback();
                throw;
            }
        }

        // ---- helpers ----
        private async Task<AuthResponseDto> IssueTokensAsync(User user)
        {
            if (user.UserRoles.Count != 1)
                throw new InvalidOperationException("La cuenta debe tener exactamente un rol antes de iniciar sesión.");
            var role = user.UserRoles.First().Role.Name.Trim();

            var access = _jwtService.GenerateToken(user.Id, user.Username, user.Email, role, user.OwnerUserId ?? user.Id, user.TokenVersion);

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
            var access = _jwtService.GenerateToken(user.Id, user.Username, user.Email, role, user.OwnerUserId ?? user.Id, user.TokenVersion);

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
