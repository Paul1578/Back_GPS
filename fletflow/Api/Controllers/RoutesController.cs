using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using fletflow.Api.Contracts.Routes;
using fletflow.Application.Fleet.Commands;
using fletflow.Application.Fleet.Dtos;
using fletflow.Application.Fleet.Queries;
using fletflow.Domain.Fleet.Entities;
using fletflow.Infrastructure.Persistence.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace fletflow.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
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
        private readonly IUnitOfWork _unitOfWork;

        [ActivatorUtilitiesConstructor]
        public RoutesController(
            CreateRouteCommand createRoute,
            UpdateRouteCommand updateRoute,
            ChangeRouteStatusCommand changeStatus,
            GetRouteByIdQuery getById,
            GetRoutesQuery getAll,
            RegisterRoutePositionCommand registerPosition,
            GetRoutePositionsQuery getPositions,
            DeleteRouteCommand deleteRoute,
            IUnitOfWork unitOfWork)
        {
            _createRoute = createRoute;
            _updateRoute = updateRoute;
            _changeStatus = changeStatus;
            _getById = getById;
            _getAll = getAll;
            _registerPosition = registerPosition;
            _getPositions = getPositions;
            _deleteRoute = deleteRoute; 
            _unitOfWork = unitOfWork;
        }
        /// <summary>
        /// Elimina una ruta por su ID.
        /// </summary>
        /// <param name="id">ID de la ruta.</param>
        /// <response code="204">Ruta eliminada correctamente.</response>
        /// <response code="404">Ruta no encontrada.</response>
        [HttpDelete("{id:guid}")]
        [Authorize(Roles = "Admin,Manager,Logistics")]
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
        [Authorize(Roles = "Admin,Manager,Logistics")]
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
        [Authorize(Roles = "Admin,Manager,Logistics")]
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
        [Authorize(Roles = "Admin,Manager,Logistics,Driver")]
        public async Task<IActionResult> ChangeStatus(Guid id, [FromBody] ChangeRouteStatusRequest request)
        {
            if (User.IsInRole("Driver"))
            {
                var route = await _unitOfWork.Routes.GetByIdAsync(id);
                if (route is null || !await IsCurrentDriverAssigned(route))
                    return NotFound();
            }

            await _changeStatus.Execute(id, request.Status);
            return NoContent();
        }

        /// <summary>
        /// Obtiene todas las rutas (opcionalmente filtradas por vehículo, conductor, estado o solo activas).
        /// Si el rol es Driver, se limita automáticamente a sus rutas.
        /// </summary>
        /// <param name="vehicleId">ID del vehículo (opcional).</param>
        /// <param name="driverId">ID del conductor (opcional).</param>
        /// <param name="status">Estado de la ruta (opcional).</param>
        /// <param name="onlyActive">Solo rutas activas (opcional).</param>
        /// <response code="200">Listado de rutas.</response>
        [HttpGet]
        [Authorize(Roles = "Admin,Manager,Logistics,Driver")]
        public async Task<ActionResult<List<RouteDto>>> GetAll(
    [FromQuery] Guid? vehicleId,
    [FromQuery] Guid? driverId,
    [FromQuery] string? status,
    [FromQuery] bool? onlyActive)   // 👈 nuevo
    {
        if (User.IsInRole("Driver"))
        {
            var userId = GetUserIdFromClaims();
            if (userId == null)
            {
                return Forbid();
            }

            var currentDriverId = await ResolveDriverIdAsync(userId.Value);
            if (currentDriverId == null)
            {
                return Forbid();
            }
            var currentDriver = await _unitOfWork.Drivers.GetByIdAsync(currentDriverId.Value);
            if (currentDriver?.IsActive != true)
            {
                return Forbid();
            }
            driverId = currentDriverId;
            vehicleId = null;
        }

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
        [Authorize(Roles = "Admin,Manager,Logistics,Driver")]
        public async Task<ActionResult<RouteDto>> GetById(Guid id)
        {
            var result = await _getById.Execute(id);
            if (User.IsInRole("Driver"))
            {
                var userId = GetUserIdFromClaims();
                if (userId == null)
                {
                    return Forbid();
                }
                var currentDriverId = await ResolveDriverIdAsync(userId.Value);
                var matchesDriver = currentDriverId != null && result.DriverId == currentDriverId.Value;
                if (!matchesDriver)
                {
                    return Forbid();
                }
            }
            return Ok(result);
        }

        [HttpPut("{routeId:guid}/points/{pointIndex:int}/status")]
        [Authorize(Roles = "Admin,Manager,Logistics,Driver")]
        public async Task<IActionResult> UpdatePointStatus(
            Guid routeId,
            int pointIndex,
            [FromBody] UpdateRoutePointStatusRequest request)
        {
            var route = await _unitOfWork.Routes.GetByIdAsync(routeId);
            if (route is null) return NotFound();
            if (User.IsInRole("Driver") && !await IsCurrentDriverAssigned(route)) return NotFound();

            route.UpdatePointStatus(pointIndex, request.Status);
            await _unitOfWork.Routes.UpdateAsync(route);
            await _unitOfWork.CommitAsync();
            return Ok(await _getById.Execute(routeId));
        }

        [HttpPost("{routeId:guid}/points/{pointIndex:int}/evidence")]
        [Authorize(Roles = "Admin,Manager,Logistics,Driver")]
        [RequestSizeLimit(6_000_000)]
        public async Task<IActionResult> AddPointEvidence(
            Guid routeId,
            int pointIndex,
            [FromBody] AddRoutePointEvidenceRequest request)
        {
            var route = await _unitOfWork.Routes.GetByIdAsync(routeId);
            if (route is null) return NotFound();
            if (User.IsInRole("Driver") && !await IsCurrentDriverAssigned(route)) return NotFound();

            var evidence = RoutePointEvidence.Create(request.Type, request.Content, request.Description);
            route.AddPointEvidence(pointIndex, evidence);
            await _unitOfWork.Routes.UpdateAsync(route);
            await _unitOfWork.CommitAsync();
            return Ok(await _getById.Execute(routeId));
        }

        [HttpPost("{routeId:guid}/positions")]
        [Authorize(Roles = "Admin,Manager,Logistics,Driver")]
        public async Task<ActionResult<RoutePositionDto>> RegisterPosition(
            Guid routeId,
            [FromBody] RegisterRoutePositionRequest request)
        {
            if (User.IsInRole("Driver"))
            {
                var userId = GetUserIdFromClaims();
                if (userId == null)
                {
                    return Forbid();
                }
                var currentDriverId = await ResolveDriverIdAsync(userId.Value);
                if (currentDriverId == null)
                {
                    return Forbid();
                }
                var route = await _getById.Execute(routeId);
                var matchesDriver = route.DriverId == currentDriverId.Value;
                var matchesLegacy = route.DriverId == userId.Value;
                if (!matchesDriver && !matchesLegacy)
                {
                    return Forbid();
                }
            }

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
        [Authorize(Roles = "Admin,Manager,Logistics,Driver")]
        public async Task<ActionResult<List<RoutePositionDto>>> GetPositions(
            Guid routeId,
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to)
        {
            if (User.IsInRole("Driver"))
            {
                var userId = GetUserIdFromClaims();
                if (userId == null)
                {
                    return Forbid();
                }
                var currentDriverId = await ResolveDriverIdAsync(userId.Value);
                if (currentDriverId == null)
                {
                    return Forbid();
                }
                var route = await _getById.Execute(routeId);
                var matchesDriver = route.DriverId == currentDriverId.Value;
                var matchesLegacy = route.DriverId == userId.Value;
                if (!matchesDriver && !matchesLegacy)
                {
                    return Forbid();
                }
            }

            var result = await _getPositions.Execute(routeId, from, to);
            return Ok(result);
        }

        private Guid? GetUserIdFromClaims()
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier) ??
                            User.FindFirst("nameid")?.Value;
            if (!Guid.TryParse(userIdStr, out var userId))
            {
                return null;
            }
            return userId;
        }

        private async Task<Guid?> ResolveDriverIdAsync(Guid userId)
        {
            var driver = await _unitOfWork.Drivers.GetByUserIdAsync(userId);
            return driver?.Id;
        }

        private async Task<bool> IsCurrentDriverAssigned(RouteE route)
        {
            var userId = GetUserIdFromClaims();
            if (userId is null) return false;
            var driverId = await ResolveDriverIdAsync(userId.Value);
            if (!driverId.HasValue) return false;
            var driver = await _unitOfWork.Drivers.GetByIdAsync(driverId.Value);
            return driver?.IsActive == true &&
                   (route.DriverId == driverId.Value || route.DriverId == userId.Value);
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
            return RoutePoint.Create(
                request.Lat,
                request.Lng,
                request.Name,
                request.Operation,
                RoutePointStatus.Pending);
        }
    }
}
