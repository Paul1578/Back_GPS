using fletflow.Domain.Fleet.Repositories;
using fletflow.Domain.Fleet.Entities;
using fletflow.Domain.Fleet.Exceptions;
using fletflow.Infrastructure.Persistence.Contracts;

namespace fletflow.Application.Fleet.Commands
{
    public class DeleteVehicleCommand
    {
        private readonly IUnitOfWork _uow;
        private readonly IVehicleRepository _vehicles;

        public DeleteVehicleCommand(IUnitOfWork uow, IVehicleRepository vehicles)
        {
            _uow = uow;
            _vehicles = vehicles;
        }

        public async Task Execute(Guid id)
        {
            try
            {
                await _uow.LockRouteAssignmentAsync(id, null);
                var vehicle = await _vehicles.GetByIdAsync(id)
                    ?? throw new KeyNotFoundException("Vehicle not found.");
                var activeRoutes = await _uow.Routes.GetAllAsync(vehicleId: id, onlyActive: true);
                if (activeRoutes.Any(route => route.Status is RouteStatus.Pending or RouteStatus.InProgress))
                    throw new BusinessConflictException("No se puede dar de baja un vehículo asignado a una ruta activa.");
                var assignedDrivers = await _uow.Drivers.GetAllAsync(onlyActive: true);
                if (assignedDrivers.Any(driver => driver.VehicleId == id))
                    throw new BusinessConflictException("Desasigna el vehículo del conductor antes de darlo de baja.");

                vehicle.Deactivate();
                await _vehicles.UpdateAsync(vehicle);
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
