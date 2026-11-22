using fletflow.Domain.Fleet.Repositories;
using fletflow.Domain.Fleet.Entities;
using fletflow.Domain.Fleet.Exceptions;
using fletflow.Infrastructure.Persistence.Contracts;

namespace fletflow.Application.Fleet.Commands
{
    public class UnassignDriverFromVehicleCommand
    {
        private readonly IUnitOfWork _uow;
        private readonly IDriverRepository _drivers;
        private readonly IRouteRepository _routes;

        public UnassignDriverFromVehicleCommand(
            IUnitOfWork uow,
            IDriverRepository drivers,
            IRouteRepository routes)
        {
            _uow = uow;
            _drivers = drivers;
            _routes = routes;
        }

        public async Task Execute(Guid driverId)
        {
            try
            {
                var driver = await _drivers.GetByIdAsync(driverId)
                    ?? throw new KeyNotFoundException("Conductor no encontrado.");
                await _uow.LockRouteAssignmentAsync(driver.VehicleId, driverId);
                driver = await _drivers.GetByIdAsync(driverId)
                    ?? throw new KeyNotFoundException("Conductor no encontrado.");
                var activeRoutes = await _routes.GetAllAsync(driverId: driverId, onlyActive: true);
                if (activeRoutes.Any(route => route.Status is RouteStatus.Pending or RouteStatus.InProgress))
                    throw new BusinessConflictException("No se puede desasignar el vehículo de un conductor con rutas activas pendientes o en progreso.");

                driver.UnassignVehicle();

                await _drivers.UpdateAsync(driver);
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
