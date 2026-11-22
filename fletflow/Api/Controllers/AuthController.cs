using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using fletflow.Application.Auth.Commands;
using fletflow.Application.Auth.Commands.ActivateAccount;
using fletflow.Application.Auth.Commands.ResetPassword;
using fletflow.Application.DTOs.Auth;
using fletflow.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;

namespace fletflow.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly AuthService _authService;
        private readonly IUserRegistrationService _registrationService;
        private readonly IEmailSender _emailService;
        private readonly ActivateAccountCommand _activateAccountCommand;
        private readonly IWebHostEnvironment _environment;

        public AuthController(AuthService authService, IUserRegistrationService registrationService, IEmailSender emailService, ActivateAccountCommand activateAccountCommand, IWebHostEnvironment environment)
        {
            _authService = authService;
            _registrationService = registrationService;
            _emailService = emailService;
            _activateAccountCommand = activateAccountCommand;
            _environment = environment;
        }

        /// <summary>
        /// Registra un nuevo usuario y devuelve tokens de acceso y refresh.
        /// </summary>
        /// <param name="dto">Datos de registro (usuario, email, contraseña, rol).</param>
        /// <returns>Tokens de acceso y refresh.</returns>
        /// <response code="200">Usuario registrado correctamente.</response>
        /// <response code="400">Datos inválidos o el correo ya fue registrado.</response>
        [HttpPost("register")]
        [AllowAnonymous]
        public async Task<IActionResult> Register([FromBody] RegisterUserDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            // El alta pública crea la cuenta raíz Admin de una empresa nueva.
            // El rol es fijo en el servidor y nunca se toma del request; owner_id queda
            // asociado al usuario recién creado para aislar su empresa del resto.
            var response = await _registrationService.RegisterAsync(dto.Username, dto.Email, dto.Password, "Admin");
            return Ok(response);
        }

        /// <summary>
        /// Inicia sesión y devuelve tokens de acceso y refresh.
        /// </summary>
        /// <param name="dto">Credenciales de usuario.</param>
        /// <returns>Tokens de acceso y refresh.</returns>
        /// <response code="200">Login exitoso.</response>
        /// <response code="400">El usuario no existe o la cuenta no está activada.</response>
        /// <response code="401">Contraseña incorrecta.</response>
        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login([FromBody] LoginUserDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var response = await _authService.LoginAsync(dto.Email, dto.Password);
            return Ok(response);
        }

        /// <summary>
        /// Cambia la contraseña del usuario autenticado y revoca todos los refresh tokens.
        /// </summary>
        /// <param name="command">Comando de cambio de contraseña.</param>
        /// <param name="dto">Datos de cambio de contraseña.</param>
        /// <returns>Mensaje de éxito.</returns>
        /// <response code="200">Contraseña cambiada correctamente.</response>
        /// <response code="401">No autenticado.</response>
        [HttpPost("change-password")]
        [Authorize]
        public async Task<IActionResult> ChangePassword(
            [FromServices] ChangePasswordCommand command,
            [FromBody] ChangePasswordDto dto)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userIdStr))
                return Unauthorized("Token sin identificador de usuario.");

            var userId = Guid.Parse(userIdStr);
            await command.Execute(userId, dto.CurrentPassword, dto.NewPassword);
            return Ok(new { message = "Contrasena actualizada correctamente." });
        }

        public class RefreshRequestDto
        {
            public string RefreshToken { get; set; } = default!;
        }

        /// <summary>
        /// Renueva el token de acceso usando un refresh token válido (rotación segura).
        /// </summary>
        /// <param name="dto">Refresh token actual.</param>
        /// <returns>Nuevos tokens de acceso y refresh.</returns>
        /// <response code="200">Tokens renovados correctamente.</response>
        /// <response code="401">Refresh token inválido o revocado.</response>
        /// <response code="401">Refresh token inválido o revocado.</response>
        /// <response code="404">Usuario no encontrado.</response>
        [HttpPost("refresh")]
        [AllowAnonymous]
        public async Task<IActionResult> Refresh([FromBody] RefreshRequestDto dto)
        {
            var response = await _authService.RefreshAsync(dto.RefreshToken);
            return Ok(response);
        }

        /// <summary>
        /// Revoca el refresh token actual (logout de una sesión).
        /// </summary>
        /// <param name="dto">Refresh token a revocar.</param>
        /// <returns>Mensaje de éxito.</returns>
        /// <response code="200">Sesión cerrada correctamente.</response>
        /// <response code="401">No autenticado.</response>
        [HttpPost("logout")]
        [Authorize]
        public async Task<IActionResult> Logout([FromBody] RefreshRequestDto dto)
        {
            await _authService.LogoutAsync(dto.RefreshToken);
            return Ok(new { message = "Sesion cerrada." });
        }

        /// <summary>
        /// Revoca todos los refresh tokens del usuario (logout global).
        /// </summary>
        /// <returns>Mensaje de éxito.</returns>
        /// <response code="200">Todas las sesiones cerradas correctamente.</response>
        /// <response code="401">No autenticado.</response>
        [HttpPost("logout-all")]
        [Authorize]
        public async Task<IActionResult> LogoutAll()
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userIdStr))
                return Unauthorized("Token sin identificador de usuario.");

            var userId = Guid.Parse(userIdStr);
            await _authService.LogoutAllAsync(userId);
            return Ok(new { message = "Todas las sesiones han sido cerradas." });
        }

        /// <summary>
        /// Obtiene la información del usuario autenticado (incluye driverId si aplica).
        /// </summary>
        /// <returns>Datos del usuario (y driverId si existe).</returns>
        /// <response code="200">Datos del usuario autenticado.</response>
        /// <response code="401">No autenticado.</response>
        /// <response code="404">Usuario no encontrado.</response>
        [HttpGet("me")]
        [Authorize]
        public async Task<IActionResult> Me()
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userIdStr))
                return Unauthorized("Token sin identificador de usuario.");

            if (!Guid.TryParse(userIdStr, out var userId))
                return Unauthorized("Identificador de usuario invalido en el token.");

            var me = await _authService.GetMeAsync(userId);
            return Ok(me);
        }

        /// <summary>
        /// Envía un correo para restablecer la contraseña del usuario.
        /// </summary>
        /// <param name="dto">Email del usuario.</param>
        /// <returns>Mensaje de éxito.</returns>
        /// <response code="200">Correo enviado si el email existe.</response>
        [HttpPost("forgot-password")]
        [AllowAnonymous]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto dto)
        {
            var resp = await _authService.ForgotPasswordAsync(dto.Email, returnTokenInResponse: false);
            return Ok(resp);
        }

        /// <summary>
        /// Restablece la contraseña usando un token recibido por correo.
        /// </summary>
        /// <param name="command">Comando de reset de contraseña.</param>
        /// <param name="dto">Datos de restablecimiento.</param>
        /// <returns>Mensaje de éxito.</returns>
        /// <response code="200">Contraseña restablecida correctamente.</response>
        /// <response code="400">Token inválido o expirado.</response>
        /// <response code="200">Contraseña restablecida correctamente.</response>
        /// <response code="400">Token inválido o expirado.</response>
        /// <response code="404">Usuario no encontrado.</response>
        [HttpPost("reset-password")]
        [AllowAnonymous]
        public async Task<IActionResult> ResetPassword([FromServices] ResetPasswordCommand command, [FromBody] ResetPasswordDto dto)
        {
            await command.ExecuteAsync(dto.Token, dto.NewPassword, dto.ConfirmNewPassword);
            return Ok(new { message = "Contrasena restablecida." });
        }

        /// <summary>
        /// Activa la cuenta de usuario y establece una nueva contraseña.
        /// </summary>
        /// <param name="dto">Token de activación y nueva contraseña.</param>
        /// <returns>Mensaje de éxito.</returns>
        /// <response code="200">Cuenta activada correctamente.</response>
        /// <response code="400">Token inválido o expirado.</response>
        [HttpPost("activate-account")]
        [AllowAnonymous]
        public async Task<IActionResult> ActivateAccount([FromBody] ActivateAccountDto dto)
        {
            await _activateAccountCommand.ExecuteAsync(dto.Token, dto.NewPassword, dto.ConfirmNewPassword);
            return Ok(new { message = "Cuenta activada y contrasena actualizada." });
        }

        /// <summary>
        /// Envía un correo de prueba para verificar la configuración de email.
        /// </summary>
        /// <param name="to">Email de destino.</param>
        /// <returns>Mensaje de éxito.</returns>
        /// <response code="200">Correo de prueba enviado.</response>
        /// <response code="400">Email de destino no proporcionado.</response>
        [HttpGet("test-email")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> TestEmail([FromQuery] string to)
        {
            if (!_environment.IsDevelopment())
                return NotFound();

            if (string.IsNullOrWhiteSpace(to))
                return BadRequest(new { message = "Debe enviar ?to=email@dominio.com" });

            var authenticatedEmail = User.FindFirstValue(JwtRegisteredClaimNames.Email)
                ?? User.FindFirstValue(ClaimTypes.Email);
            if (string.IsNullOrWhiteSpace(authenticatedEmail) ||
                !string.Equals(to.Trim(), authenticatedEmail, StringComparison.OrdinalIgnoreCase))
                return Forbid();

            var plainToken = $"test-{Guid.NewGuid():N}";
            var encoded = Uri.EscapeDataString(plainToken);
            var testLink = $"https://example.com/reset?token={encoded}";
            await _emailService.SendPasswordResetEmailAsync(authenticatedEmail, testLink, plainToken);
            return Ok(new { message = "Correo de prueba enviado (si SMTP se configuro correctamente)." });
        }
    }
}
