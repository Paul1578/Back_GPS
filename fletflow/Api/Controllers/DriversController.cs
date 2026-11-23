using fletflow.Api.Contracts.Drivers;
using System.Security.Claims;
using fletflow.Application.Fleet.Commands;
using fletflow.Application.Fleet.Dtos;
using fletflow.Application.Fleet.Queries;
using fletflow.Application.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace fletflow.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin,Manager")] // ajusta según tu modelo
    public class DriversController : ControllerBase
    {
        private readonly CreateDriverCommand _createDriver;
        private readonly GetDriverByIdQuery _getById;
        private readonly GetDriversQuery _getAll;
        
        private readonly AssignDriverToVehicleCommand _assignDriverToVehicle;
        private readonly UnassignDriverFromVehicleCommand _unassignDriverFromVehicle;
        private readonly UpdateDriverCommand _updateDriver;
        private readonly ChangeDriverStatusCommand _changeStatus;
        private readonly UnlinkDriverFromUserCommand _unlinkDriverFromUser;

        public DriversController(
            CreateDriverCommand createDriver,
            GetDriverByIdQuery getById,
            GetDriversQuery getAll,
            AssignDriverToVehicleCommand assignDriverToVehicle,
            UnassignDriverFromVehicleCommand unassignDriverFromVehicle,
            UpdateDriverCommand updateDriver,
            ChangeDriverStatusCommand changeStatus,
            UnlinkDriverFromUserCommand unlinkDriverFromUser)
        {
            _createDriver = createDriver;
            _getById = getById;
            _getAll = getAll;
            _assignDriverToVehicle = assignDriverToVehicle;
            _unassignDriverFromVehicle = unassignDriverFromVehicle;
            _updateDriver = updateDriver;
            _changeStatus = changeStatus;
            _unlinkDriverFromUser = unlinkDriverFromUser;
        }

        /// <summary>
        /// Crea un nuevo conductor.
        /// </summary>
        /// <param name="request">Datos del conductor.</param>
        /// <response code="201">Conductor creado correctamente.</response>
        /// <response code="400">Datos inválidos.</response>
        [HttpPost]
        public async Task<ActionResult<DriverDto>> Create([FromBody] CreateDriverRequest request)
        {
            if (!Guid.TryParse(User.FindFirstValue("owner_id"), out var ownerUserId))
                return Unauthorized();

            var result = await _createDriver.Execute(
                request.FirstName,
                request.LastName,
                request.DocumentNumber,
                request.PhoneNumber,
                request.UserId,
                ownerUserId
            );

            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }

        /// <summary>
        /// Obtiene todos los conductores.
        /// </summary>
        /// <param name="onlyActive">Solo activos (opcional).</param>
        /// <response code="200">Listado de conductores.</response>
        [HttpGet]
        public async Task<ActionResult<List<DriverDto>>> GetAll([FromQuery] bool? onlyActive)
        {
            var result = await _getAll.Execute(onlyActive);
            return Ok(result);
        }

        [HttpGet("page")]
        public async Task<IActionResult> GetPage(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = PaginationLimits.DefaultPageSize,
            [FromQuery] string? search = null,
            [FromQuery] bool? onlyActive = null)
        {
            if (!PaginationLimits.IsValid(page, pageSize))
                return BadRequest("page debe ser >= 1 y pageSize debe estar entre 1 y 100.");

            var result = await _getAll.ExecutePage(page, pageSize, search, onlyActive);
            return Ok(result);
        }

        /// <summary>
        /// Obtiene un conductor por ID.
        /// </summary>
        /// <param name="id">ID del conductor.</param>
        /// <response code="200">Conductor encontrado.</response>
        /// <response code="404">Conductor no encontrado.</response>
        [HttpGet("{id:guid}")]
        public async Task<ActionResult<DriverDto>> GetById(Guid id)
        {
            var result = await _getById.Execute(id);
            return Ok(result);
        }

        [HttpPut("{id:guid}")]
        public async Task<ActionResult<DriverDto>> Update(Guid id, [FromBody] UpdateDriverRequest request)
        {
            var result = await _updateDriver.Execute(
                id, request.FirstName, request.LastName, request.DocumentNumber, request.PhoneNumber);
            return Ok(result);
        }

        [HttpPut("{id:guid}/status")]
        public async Task<IActionResult> ChangeStatus(Guid id, [FromBody] ChangeDriverStatusRequest request)
        {
            if (!request.IsActive.HasValue)
                return BadRequest("El estado del conductor es obligatorio.");
            await _changeStatus.Execute(id, request.IsActive.Value);
            return NoContent();
        }
        /// <summary>
        /// Asigna un vehículo a un conductor.
        /// </summary>
        /// <param name="id">ID del conductor.</param>
        /// <param name="request">ID del vehículo.</param>
        /// <response code="204">Vehículo asignado correctamente.</response>
        /// <response code="404">Conductor o vehículo no encontrado.</response>
        [HttpPut("{id:guid}/assign-vehicle")]
        public async Task<IActionResult> AssignVehicle(Guid id, [FromBody] AssignVehicleRequest request)
        {
            await _assignDriverToVehicle.Execute(id, request.VehicleId);
            return NoContent();
        }

        // PUT api/drivers/{id}/unassign-vehicle
        /// <summary>
        /// Desasigna el vehículo de un conductor.
        /// </summary>
        /// <param name="id">ID del conductor.</param>
        /// <response code="204">Vehículo desasignado correctamente.</response>
        /// <response code="404">Conductor no encontrado.</response>
        [HttpPut("{id:guid}/unassign-vehicle")]
        public async Task<IActionResult> UnassignVehicle(Guid id)
        {
            await _unassignDriverFromVehicle.Execute(id);
            return NoContent();
        }

        /// <summary>
        /// Desvincula la cuenta de usuario del perfil de conductor, conservando ambos registros y el rol Driver.
        /// </summary>
        [HttpPut("{id:guid}/unlink-user")]
        public async Task<IActionResult> UnlinkUser(Guid id)
        {
            await _unlinkDriverFromUser.Execute(id);
            return NoContent();
        }
    }
}
