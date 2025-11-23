using System.Security.Claims;
using fletflow.Application.Users.Queries;
using fletflow.Application.Users.Commands;
using fletflow.Application.Auth.Commands;
using fletflow.Api.Contracts.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace fletflow.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin")] // "Gerente" en el front
    public class UsersController : ControllerBase
    {
        private readonly GetAllUsersQuery _getAllUsersQuery;
        private readonly UpdateUserCommand _updateUserCommand;
        private readonly AssignRoleToUserCommand _assignRole;
        private readonly RemoveRoleFromUserCommand _removeRole;
        private readonly RegisterUserCommand _registerUserCommand;
        private readonly GetMyTeamUsersQuery _getMyTeamUsersQuery;

        public UsersController(
            GetAllUsersQuery getAllUsersQuery,
            GetMyTeamUsersQuery getMyTeamUsersQuery,
            UpdateUserCommand updateUserCommand,
            AssignRoleToUserCommand assignRole,
            RemoveRoleFromUserCommand removeRole,
            RegisterUserCommand registerUserCommand)
        {
            _getAllUsersQuery = getAllUsersQuery;
            _updateUserCommand = updateUserCommand;
            _assignRole = assignRole;
            _removeRole = removeRole;
            _registerUserCommand = registerUserCommand;
            _getMyTeamUsersQuery = getMyTeamUsersQuery;
        }

        private Guid GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("nameid");

            if (claim == null || !Guid.TryParse(claim.Value, out var userId))
                throw new InvalidOperationException("No se pudo resolver el usuario autenticado.");

            return userId;
        }

        [HttpGet("my-team")]
        public async Task<IActionResult> GetMyTeam()
        {
            var ownerId = GetCurrentUserId(); // del token (nameid)

            var users = await _getMyTeamUsersQuery.Execute(ownerId);

            return Ok(users);
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var users = await _getAllUsersQuery.Execute();
            return Ok(users);
        }

        [HttpPut("{id:guid}/status")]
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

        // POST /api/users
        [HttpPost]
        public async Task<IActionResult> CreateUserWithRole([FromBody] CreateUserWithRoleRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Username))
                return BadRequest("El nombre de usuario es obligatorio.");

            if (string.IsNullOrWhiteSpace(request.Email))
                return BadRequest("El email es obligatorio.");

            if (string.IsNullOrWhiteSpace(request.Password))
                return BadRequest("La contraseña es obligatoria.");

            if (string.IsNullOrWhiteSpace(request.RoleName))
                return BadRequest("El rol es obligatorio.");

            var ownerId = GetCurrentUserId(); // Admin que crea al usuario

            var user = await _registerUserCommand.Execute(
                request.Username,
                request.Email,
                request.Password,
                request.RoleName,
                ownerId // 👈 se guarda en OwnerUserId
            );

            return Ok(new
            {
                user.Id,
                user.Username,
                user.Email,
                Role = request.RoleName,
                user.OwnerUserId
            });
        }
    }
}
