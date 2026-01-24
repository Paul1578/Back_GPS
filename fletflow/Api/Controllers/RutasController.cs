using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;


namespace fletflow.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class RoutesPRUEBAController : ControllerBase
{
    /// <summary>
    /// Obtiene rutas solo para administradores.
    /// </summary>
    /// <response code="200">Rutas para administradores.</response>
    /// <response code="401">No autenticado.</response>
    /// <response code="403">No autorizado.</response>
    [HttpGet("admin")]
    [Authorize(Roles = "Admin")]
    public IActionResult GetAdminRoutes()
    {
        return Ok("Solo los administradores pueden ver esto.");
    }

    /// <summary>
    /// Obtiene rutas solo para conductores.
    /// </summary>
    /// <response code="200">Rutas para conductores.</response>
    /// <response code="401">No autenticado.</response>
    /// <response code="403">No autorizado.</response>
    [HttpGet("driver")]
    [Authorize(Roles = "Driver")]
    public IActionResult GetDriverRoutes()
    {
        return Ok("Solo los conductores pueden ver esto.");
    }

    /// <summary>
    /// Obtiene rutas para cualquier usuario autenticado.
    /// </summary>
    /// <response code="200">Rutas para cualquier usuario autenticado.</response>
    /// <response code="401">No autenticado.</response>
    [HttpGet("any")]
    [Authorize] 
    public IActionResult GetAll()
    {
        return Ok("Cualquier usuario autenticado puede ver esto.");
    }
}

}
