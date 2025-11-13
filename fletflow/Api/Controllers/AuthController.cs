using fletflow.Infrastructure.Services;
using fletflow.Application.DTOs.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using fletflow.Application.Auth.Commands;
using System.Security.Claims;

namespace fletflow.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly AuthService _authService;

        public AuthController(AuthService authService)
        {
            _authService = authService;
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
            return Ok(new { message = "Contraseña actualizada correctamente." });
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
            return Ok(new { message = "Sesión cerrada." });
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
                return Unauthorized("Identificador de usuario inválido en el token.");

            var me = await _authService.GetMeAsync(userId);
            return Ok(me);
        }

        [HttpPost("forgot-password")]
        [AllowAnonymous]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto dto)
        {
            var resp = await _authService.ForgotPasswordAsync(dto.Email, returnTokenInResponse: true); // en prod: false
            return Ok(resp);
        }

        [HttpPost("reset-password")]
        [AllowAnonymous]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto dto)
        {
            await _authService.ResetPasswordAsync(dto.Token, dto.NewPassword);
            return Ok(new { message = "Contraseña restablecida." });
        }
    }
}
