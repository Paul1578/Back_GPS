using fletflow.Api.Contracts.Drivers;
using fletflow.Application.Fleet.Commands;
using fletflow.Application.Fleet.Dtos;
using fletflow.Application.Fleet.Queries;
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

        public DriversController(
            CreateDriverCommand createDriver,
            GetDriverByIdQuery getById,
            GetDriversQuery getAll,
            AssignDriverToVehicleCommand assignDriverToVehicle,
            UnassignDriverFromVehicleCommand unassignDriverFromVehicle)
        {
            _createDriver = createDriver;
            _getById = getById;
            _getAll = getAll;
            _assignDriverToVehicle = assignDriverToVehicle;
            _unassignDriverFromVehicle = unassignDriverFromVehicle;
        }

        [HttpPost]
        public async Task<ActionResult<DriverDto>> Create([FromBody] CreateDriverRequest request)
        {
            var result = await _createDriver.Execute(
                request.FirstName,
                request.LastName,
                request.DocumentNumber,
                request.PhoneNumber
            );

            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }

        [HttpGet]
        public async Task<ActionResult<List<DriverDto>>> GetAll([FromQuery] bool? onlyActive)
        {
            var result = await _getAll.Execute(onlyActive);
            return Ok(result);
        }

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<DriverDto>> GetById(Guid id)
        {
            var result = await _getById.Execute(id);
            return Ok(result);
        }
        [HttpPut("{id:guid}/assign-vehicle")]
        public async Task<IActionResult> AssignVehicle(Guid id, [FromBody] AssignVehicleRequest request)
        {
            await _assignDriverToVehicle.Execute(id, request.VehicleId);
            return NoContent();
        }

        // PUT api/drivers/{id}/unassign-vehicle
        [HttpPut("{id:guid}/unassign-vehicle")]
        public async Task<IActionResult> UnassignVehicle(Guid id)
        {
            await _unassignDriverFromVehicle.Execute(id);
            return NoContent();
        }
    }
}
