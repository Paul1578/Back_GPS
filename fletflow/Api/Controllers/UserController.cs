using fletflow.Application.Users.Queries;
using fletflow.Application.Users.Commands;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace fletflow.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin")]
    public class UsersController : ControllerBase
    {
        private readonly GetAllUsersQuery _getAllUsersQuery;
        private readonly UpdateUserCommand _updateUserCommand;

        public UsersController(GetAllUsersQuery getAllUsersQuery, UpdateUserCommand updateUserCommand)
        {
            _getAllUsersQuery = getAllUsersQuery;
            _updateUserCommand = updateUserCommand;
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
    }
}


