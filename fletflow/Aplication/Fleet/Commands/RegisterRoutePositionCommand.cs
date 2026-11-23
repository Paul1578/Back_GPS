using fletflow.Application.Fleet.Dtos;
using fletflow.Application.Fleet.Mappings;
using fletflow.Domain.Fleet.Entities;
using fletflow.Domain.Fleet.Repositories;
using fletflow.Infrastructure.Persistence.Contracts;

namespace fletflow.Application.Fleet.Commands
{
    public class RegisterRoutePositionCommand
    {
        private readonly IUnitOfWork _uow;
        private readonly IRouteRepository _routes;
        private readonly IRoutePositionRepository _positions;
        private readonly IDriverRepository _drivers;

        public RegisterRoutePositionCommand(
            IUnitOfWork uow,
            IRouteRepository routes,
            IRoutePositionRepository positions,
            IDriverRepository drivers)
        {
            _uow = uow;
            _routes = routes;
            _positions = positions;
            _drivers = drivers;
        }

        public async Task<RoutePositionDto> Execute(
            Guid routeId,
            double latitude,
            double longitude,
            DateTime? recordedAt,
            double? speedKmh,
            double? heading,
            Guid? requestingDriverId = null,
            Guid? requestingUserId = null)
        {
            var route = await _routes.GetByIdAsync(routeId)
                ?? throw new KeyNotFoundException("Ruta no encontrada.");

            try
            {
                await _uow.LockRouteAssignmentAsync(route.VehicleId, route.DriverId, routeId);
                route = await _routes.GetByIdAsync(routeId)
                    ?? throw new KeyNotFoundException("Ruta no encontrada.");

                if (requestingDriverId.HasValue)
                {
                    var driver = await _drivers.GetByIdAsync(requestingDriverId.Value);
                    var stillAssigned = route.DriverId == requestingDriverId.Value
                        || requestingUserId.HasValue && route.DriverId == requestingUserId.Value;
                    if (driver?.IsActive != true || !stillAssigned)
                        throw new UnauthorizedAccessException("El conductor ya no está asignado a esta ruta.");
                }

                if (!route.IsActive)
                    throw new InvalidOperationException("No se puede registrar posiciones en una ruta inactiva.");

                if (route.Status != RouteStatus.InProgress)
                    throw new InvalidOperationException("Solo se pueden registrar posiciones mientras la ruta está en progreso.");

                var position = RoutePosition.Create(
                    routeId,
                    latitude,
                    longitude,
                    recordedAt,
                    speedKmh,
                    heading
                );

                await _positions.AddAsync(position);
                await _uow.CommitAsync();

                return RoutePositionApplicationMapper.ToDto(position);
            }
            catch
            {
                _uow.Rollback();
                throw;
            }
        }
    }
}
