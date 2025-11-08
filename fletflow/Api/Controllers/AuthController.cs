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
            var token = await _authService.RegisterAsync(dto.Username, dto.Email, dto.Password, dto.RoleName);

            var response = new AuthResponseDto
            {
                Token = token,
                Expiration = DateTime.UtcNow.AddHours(6)
            };

            return Ok(response);
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginUserDto dto)
        {
            var token = await _authService.LoginAsync(dto.Email, dto.Password);

            var response = new AuthResponseDto
            {
                Token = token,
                Expiration = DateTime.UtcNow.AddHours(6)
            };

            return Ok(response);
        }
    }
}
