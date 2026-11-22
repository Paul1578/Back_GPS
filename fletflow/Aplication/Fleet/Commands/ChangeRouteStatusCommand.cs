using fletflow.Domain.Fleet.Entities;
using fletflow.Domain.Fleet.Exceptions;
using fletflow.Domain.Fleet.Repositories;
using fletflow.Infrastructure.Persistence.Contracts;
using System.Linq;

namespace fletflow.Application.Fleet.Commands
{
    public class ChangeRouteStatusCommand
    {
        private readonly IUnitOfWork _uow;
        private readonly IRouteRepository _routes;
        private readonly IVehicleRepository _vehicles;
        private readonly IDriverRepository _drivers;

        public ChangeRouteStatusCommand(
            IUnitOfWork uow,
            IRouteRepository routes,
            IVehicleRepository vehicles,
            IDriverRepository drivers)
        {
            _uow = uow;
            _routes = routes;
            _vehicles = vehicles;
            _drivers = drivers;
        }

        public async Task Execute(Guid id, RouteStatus status)
        {
            if (!Enum.IsDefined(typeof(RouteStatus), status))
                throw new ArgumentException("Estado de ruta no válido.", nameof(status));

            var route = await _routes.GetByIdAsync(id)
                ?? throw new KeyNotFoundException("Route not found.");

            try
            {
                // Bloquea la ruta y su asignación vigente antes de volver a leer su estado.
                await _uow.LockRouteAssignmentAsync(route.VehicleId, route.DriverId, id);
                route = await _routes.GetByIdAsync(id)
                    ?? throw new KeyNotFoundException("Route not found.");

                if (!route.IsActive)
                    throw new InvalidOperationException("Cannot change the status of an inactive route.");

                var transitionAllowed = route.Status switch
                {
                    RouteStatus.Pending => status is RouteStatus.InProgress or RouteStatus.Cancelled,
                    RouteStatus.InProgress => status is RouteStatus.Pending or RouteStatus.Completed or RouteStatus.Cancelled,
                    _ => false,
                };
                if (!transitionAllowed)
                    throw new BusinessConflictException($"No se permite cambiar una ruta de {route.Status} a {status}.");

                var vehicle = await _vehicles.GetByIdAsync(route.VehicleId)
                    ?? throw new KeyNotFoundException("Vehicle associated with the route not found.");

                if (status == RouteStatus.InProgress)
                {
                    if (!vehicle.IsActive)
                        throw new BusinessConflictException("No se puede iniciar una ruta con un vehículo inactivo.");

                    var driver = await _drivers.GetByIdAsync(route.DriverId)
                        ?? throw new KeyNotFoundException("Driver assigned to the route not found.");
                    if (!driver.IsActive)
                        throw new BusinessConflictException("No se puede iniciar una ruta con un conductor inactivo.");

                    var otherInProgressRoutes = await _routes.GetAllAsync(
                        vehicleId: route.VehicleId,
                        status: RouteStatus.InProgress,
                        onlyActive: true);
                    if (otherInProgressRoutes.Any(other => other.Id != route.Id))
                        throw new BusinessConflictException("El vehículo ya tiene otra ruta en progreso.");

                    if (vehicle.Status != VehicleStatus.Available && route.Status != RouteStatus.InProgress)
                        throw new BusinessConflictException("El vehículo no está disponible para iniciar esta ruta.");

                    vehicle.SetStatus(VehicleStatus.OnRoute);
                }
                else if (status is RouteStatus.Completed or RouteStatus.Cancelled ||
                         status == RouteStatus.Pending && route.Status == RouteStatus.InProgress)
                {
                    var otherInProgressRoutes = await _routes.GetAllAsync(
                        vehicleId: route.VehicleId,
                        status: RouteStatus.InProgress,
                        onlyActive: true);
                    if (!otherInProgressRoutes.Any(other => other.Id != route.Id))
                        vehicle.SetStatus(VehicleStatus.Available);
                }

                route.ChangeStatus(status);
                await _vehicles.UpdateAsync(vehicle);
                await _routes.UpdateAsync(route);
                await _uow.CommitAsync();
            }
            catch
            {
                _uow.Rollback();
                throw;
            }
        }
    }
}
