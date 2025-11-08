using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace fletflow.Api.Controllers
{
    [ApiController]
[Route("api/[controller]")]
public class RoutesController : ControllerBase
{
    [HttpGet("admin")]
    [Authorize(Roles = "Admin")]
    public IActionResult GetAdminRoutes()
    {
        return Ok("Solo los administradores pueden ver esto.");
    }

    [HttpGet("driver")]
    [Authorize(Roles = "Driver")]
    public IActionResult GetDriverRoutes()
    {
        return Ok("Solo los conductores pueden ver esto.");
    }

    [HttpGet("any")]
    [Authorize] 
    public IActionResult GetAll()
    {
        return Ok("Cualquier usuario autenticado puede ver esto.");
    }
}

}
