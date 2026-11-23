using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using fletflow.Application.Auth.Queries;


namespace fletflow.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin")]
    public class RolesController : ControllerBase
    {
        private readonly GetAllRolesQuery _getAll;

        public RolesController(GetAllRolesQuery getAll)
        {
            _getAll = getAll;
        }

        /// <summary>
        /// Obtiene todos los roles.
        /// </summary>
        /// <response code="200">Listado de roles.</response>
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var roles = await _getAll.Execute();
            return Ok(roles);
        }

        /// <summary>
        /// La creación está reservada a una futura administración de plataforma.
        /// </summary>
        /// <response code="403">Un Admin de empresa no puede modificar el catálogo global.</response>
        [HttpPost]
        public IActionResult Create()
        {
            // Roles are a shared global catalog. Tenant Admins must not mutate it.
            return Forbid();
        }

        /// <summary>
        /// La eliminación está reservada a una futura administración de plataforma.
        /// </summary>
        /// <param name="name">Nombre del rol.</param>
        /// <response code="403">Un Admin de empresa no puede modificar el catálogo global.</response>
        [HttpDelete("{name}")]
        public IActionResult Delete(string name)
        {
            // Roles are a shared global catalog. Tenant Admins must not mutate it.
            return Forbid();
        }
    }
}
