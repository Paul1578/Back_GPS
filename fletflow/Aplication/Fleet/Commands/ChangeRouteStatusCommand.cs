using fletflow.Domain.Fleet.Entities;
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

        public ChangeRouteStatusCommand(
            IUnitOfWork uow,
            IRouteRepository routes,
            IVehicleRepository vehicles)
        {
            _uow = uow;
            _routes = routes;
            _vehicles = vehicles;
        }

        public async Task Execute(Guid id, RouteStatus status)
        {
            if (!Enum.IsDefined(typeof(RouteStatus), status))
                throw new ArgumentException("Estado de ruta no válido.", nameof(status));

            // 1. Cargamos la ruta
            var route = await _routes.GetByIdAsync(id)
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
                throw new InvalidOperationException($"No se permite cambiar una ruta de {route.Status} a {status}.");

            // 2. Cargamos el vehículo asociado
            var vehicle = await _vehicles.GetByIdAsync(route.VehicleId)
                ?? throw new KeyNotFoundException("Vehicle associated with the route not found.");

            // 3. Regla de negocio: estado de ruta → estado de vehículo
            switch (status)
            {
                case RouteStatus.InProgress:
                    if (vehicle.Status != VehicleStatus.Available && route.Status != RouteStatus.InProgress)
                        throw new InvalidOperationException("El vehículo no está disponible para iniciar esta ruta.");
                    vehicle.SetStatus(VehicleStatus.OnRoute);
                    break;

                case RouteStatus.Completed:
                case RouteStatus.Cancelled:
                    var otherInProgressRoutes = await _routes.GetAllAsync(
                        vehicleId: route.VehicleId,
                        status: RouteStatus.InProgress,
                        onlyActive: true);
                    if (!otherInProgressRoutes.Any(other => other.Id != route.Id))
                        vehicle.SetStatus(VehicleStatus.Available);
                    break;

                case RouteStatus.Pending:
                    // Pausar libera el vehículo cuando no hay otra ruta activa usándolo.
                    if (route.Status == RouteStatus.InProgress)
                    {
                        var otherActiveRoutes = await _routes.GetAllAsync(
                            vehicleId: route.VehicleId,
                            status: RouteStatus.InProgress,
                            onlyActive: true);
                        if (!otherActiveRoutes.Any(other => other.Id != route.Id))
                            vehicle.SetStatus(VehicleStatus.Available);
                    }
                    break;
            }

            // 4. Cambiamos el estado de la ruta
            route.ChangeStatus(status);

            // 5. Persistimos ambos cambios en una sola transacción
            await _vehicles.UpdateAsync(vehicle);
            await _routes.UpdateAsync(route);
            await _uow.CommitAsync();
        }
    }
}
