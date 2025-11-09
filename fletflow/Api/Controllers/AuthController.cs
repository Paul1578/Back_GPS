using fletflow.Infrastructure.Services;
using fletflow.Application.DTOs.Auth;
using Microsoft.AspNetCore.Mvc;

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
        public async Task<IActionResult> Register([FromBody] RegisterUserDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var token = await _authService.RegisterAsync(dto.Username, dto.Email, dto.Password, dto.RoleName);

                return Ok(new AuthResponseDto
                {
                    Token = token,
                    Expiration = DateTime.UtcNow.AddHours(6)
                });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { message = ex.Message }); // 409
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Error interno: {ex.Message}" });
            }
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginUserDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var token = await _authService.LoginAsync(dto.Email, dto.Password);

                return Ok(new AuthResponseDto
                {
                    Token = token,
                    Expiration = DateTime.UtcNow.AddHours(6)
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message }); // 401
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Error interno: {ex.Message}" });
            }
        }
    }
}
