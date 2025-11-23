using fletflow.Domain.Fleet.Entities;
using fletflow.Domain.Fleet.Repositories;
using fletflow.Infrastructure.Persistence.Contracts;

namespace fletflow.Application.Fleet.Commands
{
    public class ChangeRouteStatusCommand
    {
        private readonly IUnitOfWork _uow;
        private readonly IRouteRepository _routes;
        private readonly IVehicleRepository _vehicles;   // 👈 NUEVO

        public ChangeRouteStatusCommand(
            IUnitOfWork uow,
            IRouteRepository routes,
            IVehicleRepository vehicles)                   // 👈 NUEVO
        {
            _uow = uow;
            _routes = routes;
            _vehicles = vehicles;                         // 👈 NUEVO
        }

        public async Task Execute(Guid id, RouteStatus status)
        {
            // 1. Cargamos la ruta
            var route = await _routes.GetByIdAsync(id)
                ?? throw new KeyNotFoundException("Ruta no encontrada.");

            if (!route.IsActive)
                throw new InvalidOperationException("No se puede cambiar el estado de una ruta inactiva.");

            // 2. Cargamos el vehículo asociado
            var vehicle = await _vehicles.GetByIdAsync(route.VehicleId)
                ?? throw new KeyNotFoundException("Vehículo asociado a la ruta no encontrado.");

            // 3. Regla de negocio: estado de ruta → estado de vehículo
            switch (status)
            {
                case RouteStatus.InProgress:
                    vehicle.SetStatus(VehicleStatus.OnRoute);
                    break;

                case RouteStatus.Completed:
                case RouteStatus.Cancelled:
                    vehicle.SetStatus(VehicleStatus.Available);
                    break;

                case RouteStatus.Pending:
                    // En este caso no tocamos el estado del vehículo
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
