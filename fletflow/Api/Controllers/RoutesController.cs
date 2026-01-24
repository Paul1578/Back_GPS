using System.Collections.Generic;
using System.Linq;
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
        private readonly DeleteRouteCommand _deleteRoute; 

        public RoutesController(
            CreateRouteCommand createRoute,
            UpdateRouteCommand updateRoute,
            ChangeRouteStatusCommand changeStatus,
            GetRouteByIdQuery getById,
            GetRoutesQuery getAll,
            RegisterRoutePositionCommand registerPosition,
            GetRoutePositionsQuery getPositions,
            DeleteRouteCommand deleteRoute)
        {
            _createRoute = createRoute;
            _updateRoute = updateRoute;
            _changeStatus = changeStatus;
            _getById = getById;
            _getAll = getAll;
            _registerPosition = registerPosition;
            _getPositions = getPositions;
            _deleteRoute = deleteRoute; 
        }
        /// <summary>
        /// Elimina una ruta por su ID.
        /// </summary>
        /// <param name="id">ID de la ruta.</param>
        /// <response code="204">Ruta eliminada correctamente.</response>
        /// <response code="404">Ruta no encontrada.</response>
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            await _deleteRoute.Execute(id);
            return NoContent();
        }

        /// <summary>
        /// Crea una nueva ruta.
        /// </summary>
        /// <param name="request">Datos de la ruta.</param>
        /// <response code="201">Ruta creada correctamente.</response>
        /// <response code="400">Datos inválidos.</response>
        [HttpPost]
        public async Task<ActionResult<RouteDto>> Create([FromBody] CreateRouteRequest request)
        {
            var points = BuildPoints(request.Points, request.Origin, request.Destination);

            var result = await _createRoute.Execute(
                request.VehicleId,
                request.DriverId,
                request.Name,
                points,
                request.CargoDescription,
                request.PlannedStart,
                request.PlannedEnd
            );

            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }

        /// <summary>
        /// Actualiza una ruta existente.
        /// </summary>
        /// <param name="id">ID de la ruta.</param>
        /// <param name="request">Datos de la ruta.</param>
        /// <response code="200">Ruta actualizada correctamente.</response>
        /// <response code="400">Datos inválidos.</response>
        /// <response code="404">Ruta no encontrada.</response>
        [HttpPut("{id:guid}")]
        public async Task<ActionResult<RouteDto>> Update(Guid id, [FromBody] UpdateRouteRequest request)
        {
            var points = BuildPoints(request.Points, request.Origin, request.Destination);

            var result = await _updateRoute.Execute(
                id,
                request.VehicleId,
                request.DriverId,
                request.Name,
                points,
                request.CargoDescription,
                request.PlannedStart,
                request.PlannedEnd
            );

            return Ok(result);
        }

        /// <summary>
        /// Cambia el estado de una ruta.
        /// </summary>
        /// <param name="id">ID de la ruta.</param>
        /// <param name="request">Nuevo estado.</param>
        /// <response code="204">Estado actualizado correctamente.</response>
        /// <response code="404">Ruta no encontrada.</response>
        [HttpPut("{id:guid}/status")]
        public async Task<IActionResult> ChangeStatus(Guid id, [FromBody] ChangeRouteStatusRequest request)
        {
            await _changeStatus.Execute(id, request.Status);
            return NoContent();
        }

        /// <summary>
        /// Obtiene todas las rutas (opcionalmente filtradas por vehículo).
        /// </summary>
        /// <param name="vehicleId">ID del vehículo (opcional).</param>
        /// <response code="200">Listado de rutas.</response>
        [HttpGet]
        public async Task<ActionResult<List<RouteDto>>> GetAll(
    [FromQuery] Guid? vehicleId,
    [FromQuery] Guid? driverId,
    [FromQuery] string? status,
    [FromQuery] bool? onlyActive)   // 👈 nuevo
    {
        RouteStatus? parsedStatus = null;

        if (!string.IsNullOrWhiteSpace(status))
        {
            
            if (Enum.TryParse<RouteStatus>(status, ignoreCase: true, out var enumStatus))
            {
                parsedStatus = enumStatus;
            }
            // Permitir número (0,1,2,3)
            else if (int.TryParse(status, out var intStatus) &&
                    Enum.IsDefined(typeof(RouteStatus), intStatus))
            {
                parsedStatus = (RouteStatus)intStatus;
            }
            else
            {
                throw new ArgumentException($"El estado de ruta '{status}' no es válido.");
            }
        }

        var result = await _getAll.Execute(
            vehicleId,
            driverId,
            parsedStatus,
            onlyActive
        );

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

        private static List<RoutePoint> BuildPoints(
            IEnumerable<RoutePointRequest>? points,
            RoutePointRequest? origin,
            RoutePointRequest? destination)
        {
            var list = points?.Select(ToRoutePoint).ToList() ?? new List<RoutePoint>();

            // Si envían origin/destination explícitos, forzamos que sean extremos.
            if (origin != null)
            {
                var originPoint = ToRoutePoint(origin);
                if (list.Count == 0)
                    list.Add(originPoint);
                else
                    list[0] = originPoint;
            }

            if (destination != null)
            {
                var destPoint = ToRoutePoint(destination);
                if (list.Count < 2)
                    list.Add(destPoint);
                else
                    list[list.Count - 1] = destPoint;
            }

            if (list.Count < 2)
                throw new ArgumentException("Debes enviar al menos origen y destino con lat/lng.");

            return list;
        }

        private static RoutePoint ToRoutePoint(RoutePointRequest request)
        {
            return RoutePoint.Create(request.Lat, request.Lng, request.Name);
        }
    }
}
