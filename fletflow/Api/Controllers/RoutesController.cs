using fletflow.Api.Contracts.Routes;
using fletflow.Application.Fleet.Commands;
using fletflow.Application.Fleet.Dtos;
using fletflow.Application.Fleet.Queries;
using fletflow.Domain.Fleet.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace fletflow.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin,Manager,Dispatcher")] // ajusta según tus roles
    public class RoutesController : ControllerBase
    {
        private readonly CreateRouteCommand _createRoute;
        private readonly UpdateRouteCommand _updateRoute;
        private readonly ChangeRouteStatusCommand _changeStatus;
        private readonly GetRouteByIdQuery _getById;
        private readonly GetRoutesQuery _getAll;

        private readonly RegisterRoutePositionCommand _registerPosition;
        private readonly GetRoutePositionsQuery _getPositions;  

        public RoutesController(
            CreateRouteCommand createRoute,
            UpdateRouteCommand updateRoute,
            ChangeRouteStatusCommand changeStatus,
            GetRouteByIdQuery getById,
            GetRoutesQuery getAll,
            RegisterRoutePositionCommand registerPosition,
            GetRoutePositionsQuery getPositions)
        {
            _createRoute = createRoute;
            _updateRoute = updateRoute;
            _changeStatus = changeStatus;
            _getById = getById;
            _getAll = getAll;
            _registerPosition = registerPosition;
            _getPositions = getPositions;
        }

        [HttpPost]
        public async Task<ActionResult<RouteDto>> Create([FromBody] CreateRouteRequest request)
        {
            var result = await _createRoute.Execute(
                request.VehicleId,
                request.DriverId,
                request.Origin,
                request.Destination,
                request.CargoDescription,
                request.PlannedStart,
                request.PlannedEnd
            );

            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }

        [HttpPut("{id:guid}")]
        public async Task<ActionResult<RouteDto>> Update(Guid id, [FromBody] UpdateRouteRequest request)
        {
            var result = await _updateRoute.Execute(
                id,
                request.VehicleId,
                request.DriverId,
                request.Origin,
                request.Destination,
                request.CargoDescription,
                request.PlannedStart,
                request.PlannedEnd
            );

            return Ok(result);
        }

        [HttpPut("{id:guid}/status")]
        public async Task<IActionResult> ChangeStatus(Guid id, [FromBody] ChangeRouteStatusRequest request)
        {
            await _changeStatus.Execute(id, request.Status);
            return NoContent();
        }

        [HttpGet]
        public async Task<ActionResult<List<RouteDto>>> GetAll(
            [FromQuery] Guid? vehicleId,
            [FromQuery] Guid? driverId,
            [FromQuery] RouteStatus? status)
        {
            var result = await _getAll.Execute(vehicleId, driverId, status);
            return Ok(result);
        }

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<RouteDto>> GetById(Guid id)
        {
            var result = await _getById.Execute(id);
            return Ok(result);
        }

        [HttpPost("{routeId:guid}/positions")]
        public async Task<ActionResult<RoutePositionDto>> RegisterPosition(
            Guid routeId,
            [FromBody] RegisterRoutePositionRequest request)
        {
            var result = await _registerPosition.Execute(
                routeId,
                request.Latitude,
                request.Longitude,
                request.RecordedAt,
                request.SpeedKmh,
                request.Heading
            );

            return Ok(result);
        }

        // GET api/routes/{routeId}/positions?from=2025-11-14T00:00:00Z&to=...
        [HttpGet("{routeId:guid}/positions")]
        public async Task<ActionResult<List<RoutePositionDto>>> GetPositions(
            Guid routeId,
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to)
        {
            var result = await _getPositions.Execute(routeId, from, to);
            return Ok(result);
        }
    }
}
