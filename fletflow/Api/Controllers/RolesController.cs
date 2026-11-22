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

        public class CreateRoleDto { public string Name { get; set; } = default!; }

        /// <summary>
        /// Crea un nuevo rol.
        /// </summary>
        /// <param name="dto">Datos del rol.</param>
        /// <response code="201">Rol creado correctamente.</response>
        /// <response code="400">Datos inválidos o rol ya existe.</response>
        [HttpPost]
        public IActionResult Create([FromBody] CreateRoleDto dto)
        {
            // Roles are a shared global catalog. Tenant Admins must not mutate it.
            return Forbid();
        }

        /// <summary>
        /// Elimina un rol por nombre.
        /// </summary>
        /// <param name="name">Nombre del rol.</param>
        /// <response code="200">Rol eliminado correctamente.</response>
        /// <response code="404">Rol no encontrado.</response>
        [HttpDelete("{name}")]
        public IActionResult Delete(string name)
        {
            // Roles are a shared global catalog. Tenant Admins must not mutate it.
            return Forbid();
        }
    }
}
