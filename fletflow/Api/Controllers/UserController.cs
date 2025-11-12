using fletflow.Application.Users.Queries;
using fletflow.Application.Users.Commands;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using fletflow.Application.Auth.Commands;

namespace fletflow.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin")]
    public class UsersController : ControllerBase
    {
        private readonly GetAllUsersQuery _getAllUsersQuery;
        private readonly UpdateUserCommand _updateUserCommand;
        private readonly AssignRoleToUserCommand _assignRole;
        private readonly RemoveRoleFromUserCommand _removeRole;

        public UsersController(GetAllUsersQuery getAllUsersQuery, UpdateUserCommand updateUserCommand, AssignRoleToUserCommand assignRole, RemoveRoleFromUserCommand removeRole)
        {
            _getAllUsersQuery = getAllUsersQuery;
            _updateUserCommand = updateUserCommand;
            _assignRole = assignRole;
            _removeRole = removeRole;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var users = await _getAllUsersQuery.Execute();
            return Ok(users);
        }

        [HttpPut("{id}/status")]
        public async Task<IActionResult> UpdateStatus(Guid id, [FromQuery] bool isActive)
        {
            await _updateUserCommand.Execute(id, isActive);
            return Ok(new { Message = "Estado del usuario actualizado." });
        }

        [HttpPost("{id:guid}/roles/{name}")]
        public async Task<IActionResult> AssignRole(Guid id, string name)
        {
            await _assignRole.Execute(id, name);
            return Ok(new { message = "Rol asignado", userId = id, role = name });
        }

        [HttpDelete("{id:guid}/roles/{name}")]
        public async Task<IActionResult> RemoveRole(Guid id, string name)
        {
            await _removeRole.Execute(id, name);
            return Ok(new { message = "Rol removido", userId = id, role = name });
        }
    }
}


