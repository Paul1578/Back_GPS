using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using fletflow.Application.Auth.Queries;
using fletflow.Application.Auth.Commands;


namespace fletflow.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin")]
    public class RolesController : ControllerBase
    {
        private readonly GetAllRolesQuery _getAll;
        private readonly CreateRoleCommand _create;
        private readonly DeleteRoleCommand _delete;

        public RolesController(GetAllRolesQuery getAll, CreateRoleCommand create, DeleteRoleCommand delete)
        {
            _getAll = getAll;
            _create = create;
            _delete = delete;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var roles = await _getAll.Execute();
            return Ok(roles);
        }

        public class CreateRoleDto { public string Name { get; set; } = default!; }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateRoleDto dto)
        {
            await _create.Execute(dto.Name);
            return Created("", new { message = "Rol creado", name = dto.Name });
        }

        [HttpDelete("{name}")]
        public async Task<IActionResult> Delete(string name)
        {
            await _delete.Execute(name);
            return Ok(new { message = "Rol eliminado", name });
        }
    }
}
