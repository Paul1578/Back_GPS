using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace fletflow.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize] 
    public class RutasController : ControllerBase
    {
        [HttpGet("mis-rutas")]
        public IActionResult GetMisRutas()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userEmail = User.FindFirstValue(ClaimTypes.Email);

            return Ok(new
            {
                Message = "Acceso autorizado ✅",
                UserId = userId,
                Email = userEmail,
                Fecha = DateTime.UtcNow
            });
        }
    }
}
