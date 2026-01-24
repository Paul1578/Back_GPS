using fletflow.Api.Contracts.Vehicles;
using fletflow.Application.Fleet.Commands;
using fletflow.Application.Fleet.Dtos;
using fletflow.Application.Fleet.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace fletflow.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin,Manager")] // ajusta según tus roles
    public class VehiclesController : ControllerBase
    {
        private readonly CreateVehicleCommand _createVehicle;
        private readonly UpdateVehicleCommand _updateVehicle;
        private readonly ChangeVehicleStatusCommand _changeStatus;
        private readonly GetVehicleByIdQuery _getById;
        private readonly GetVehiclesQuery _getAll;
        private readonly DeleteVehicleCommand _deleteVehicle;

        public VehiclesController(
            CreateVehicleCommand createVehicle,
            UpdateVehicleCommand updateVehicle,
            ChangeVehicleStatusCommand changeStatus,
            GetVehicleByIdQuery getById,
            GetVehiclesQuery getAll,
            DeleteVehicleCommand deleteVehicle)
        {
            _createVehicle = createVehicle;
            _updateVehicle = updateVehicle;
            _changeStatus = changeStatus;
            _getById = getById;
            _getAll = getAll;
            _deleteVehicle = deleteVehicle;
        }

        /// <summary>
        /// Elimina un vehículo por su ID.
        /// </summary>
        /// <param name="id">ID del vehículo.</param>
        /// <response code="204">Vehículo eliminado correctamente.</response>
        /// <response code="404">Vehículo no encontrado.</response>
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            await _deleteVehicle.Execute(id);
            return NoContent();
        }
        
        /// <summary>
        /// Crea un nuevo vehículo.
        /// </summary>
        /// <param name="request">Datos del vehículo.</param>
        /// <response code="201">Vehículo creado correctamente.</response>
        /// <response code="400">Datos inválidos.</response>
        [HttpPost]
        public async Task<ActionResult<VehicleDto>> Create([FromBody] CreateVehicleRequest request)
        {
            var result = await _createVehicle.Execute(
                request.Plate,
                request.Brand,
                request.Model,
                request.Year,
                request.Description
            );

            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }

        /// <summary>
        /// Actualiza un vehículo existente.
        /// </summary>
        /// <param name="id">ID del vehículo.</param>
        /// <param name="request">Datos del vehículo.</param>
        /// <response code="200">Vehículo actualizado correctamente.</response>
        /// <response code="400">Datos inválidos.</response>
        /// <response code="404">Vehículo no encontrado.</response>
        [HttpPut("{id:guid}")]
        public async Task<ActionResult<VehicleDto>> Update(Guid id, [FromBody] UpdateVehicleRequest request)
        {
            var result = await _updateVehicle.Execute(
                id,
                request.Plate,
                request.Brand,
                request.Model,
                request.Year,
                request.Description
            );

            return Ok(result);
        }

        /// <summary>
        /// Cambia el estado de un vehículo.
        /// </summary>
        /// <param name="id">ID del vehículo.</param>
        /// <param name="request">Nuevo estado.</param>
        /// <response code="204">Estado actualizado correctamente.</response>
        /// <response code="404">Vehículo no encontrado.</response>
        [HttpPut("{id:guid}/status")]
        public async Task<IActionResult> ChangeStatus(Guid id, [FromBody] ChangeVehicleStatusRequest request)
        {
            await _changeStatus.Execute(id, request.Status);
            return NoContent();
        }

        /// <summary>
        /// Obtiene todos los vehículos.
        /// </summary>
        /// <param name="onlyActive">Solo activos (opcional).</param>
        /// <response code="200">Listado de vehículos.</response>
        [HttpGet]
        public async Task<ActionResult<List<VehicleDto>>> GetAll([FromQuery] bool? onlyActive)
        {
            var result = await _getAll.Execute(onlyActive);
            return Ok(result);
        }

        /// <summary>
        /// Obtiene un vehículo por ID.
        /// </summary>
        /// <param name="id">ID del vehículo.</param>
        /// <response code="200">Vehículo encontrado.</response>
        /// <response code="404">Vehículo no encontrado.</response>
        [HttpGet("{id:guid}")]
        public async Task<ActionResult<VehicleDto>> GetById(Guid id)
        {
            var result = await _getById.Execute(id);
            return Ok(result);
        }
    }
}
