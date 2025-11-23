using System;
using System.Security.Claims;
using fletflow.Application.Auth.Commands;
using fletflow.Application.Auth.Commands.ResetPassword;
using fletflow.Application.DTOs.Auth;
using fletflow.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace fletflow.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly AuthService _authService;
        private readonly IEmailSender _emailService;

        public AuthController(AuthService authService, IEmailSender emailService)
        {
            _authService = authService;
            _emailService = emailService;
        }

        [HttpPost("register")]
        [AllowAnonymous]
        public async Task<IActionResult> Register([FromBody] RegisterUserDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var response = await _authService.RegisterAsync(dto.Username, dto.Email, dto.Password, dto.RoleName);
            return Ok(response);
        }

        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login([FromBody] LoginUserDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var response = await _authService.LoginAsync(dto.Email, dto.Password);
            return Ok(response);
        }

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

        [HttpPost("refresh")]
        [AllowAnonymous]
        public async Task<IActionResult> Refresh([FromBody] RefreshRequestDto dto)
        {
            var response = await _authService.RefreshAsync(dto.RefreshToken);
            return Ok(response);
        }

        [HttpPost("logout")]
        [Authorize]
        public async Task<IActionResult> Logout([FromBody] RefreshRequestDto dto)
        {
            await _authService.LogoutAsync(dto.RefreshToken);
            return Ok(new { message = "Sesion cerrada." });
        }

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

        [HttpPost("forgot-password")]
        [AllowAnonymous]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto dto)
        {
            var resp = await _authService.ForgotPasswordAsync(dto.Email, returnTokenInResponse: false);
            return Ok(resp);
        }

        [HttpPost("reset-password")]
        [AllowAnonymous]
        public async Task<IActionResult> ResetPassword([FromServices] ResetPasswordCommand command, [FromBody] ResetPasswordDto dto)
        {
            await command.ExecuteAsync(dto.Token, dto.NewPassword, dto.ConfirmNewPassword);
            return Ok(new { message = "Contrasena restablecida." });
        }

        [HttpGet("test-email")]
        [AllowAnonymous]
        public async Task<IActionResult> TestEmail([FromQuery] string to)
        {
            if (string.IsNullOrWhiteSpace(to))
                return BadRequest(new { message = "Debe enviar ?to=email@dominio.com" });

            var plainToken = $"test-{Guid.NewGuid():N}";
            var encoded = Uri.EscapeDataString(plainToken);
            var testLink = $"https://example.com/reset?token={encoded}";
            await _emailService.SendPasswordResetEmailAsync(to, testLink, plainToken);
            return Ok(new { message = "Correo de prueba enviado (si SMTP se configuro correctamente)." });
        }
    }
}
