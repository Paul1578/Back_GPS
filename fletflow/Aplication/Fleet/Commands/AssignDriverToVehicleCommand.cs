using fletflow.Domain.Fleet.Repositories;
using fletflow.Domain.Fleet.Entities;
using fletflow.Infrastructure.Persistence.Contracts;

namespace fletflow.Application.Fleet.Commands
{
    public class AssignDriverToVehicleCommand
    {
        private readonly IUnitOfWork _uow;
        private readonly IDriverRepository _drivers;
        private readonly IVehicleRepository _vehicles;

        public AssignDriverToVehicleCommand(
            IUnitOfWork uow,
            IDriverRepository drivers,
            IVehicleRepository vehicles)
        {
            _uow = uow;
            _drivers = drivers;
            _vehicles = vehicles;
        }

        public async Task Execute(Guid driverId, Guid vehicleId)
        {
            try
            {
                await _uow.LockRouteAssignmentAsync(vehicleId, driverId);
                var driver = await _drivers.GetByIdAsync(driverId)
                    ?? throw new KeyNotFoundException("Driver not found.");

                var vehicle = await _vehicles.GetByIdAsync(vehicleId)
                    ?? throw new KeyNotFoundException("Vehicle not found.");

                if (!driver.IsActive)
                    throw new InvalidOperationException("Cannot assign an inactive driver.");

                if (driver.UserId.HasValue)
                {
                    var linkedUser = await _uow.Users.GetByIdAsync(driver.UserId.Value);
                    if (linkedUser is null || !linkedUser.IsActive)
                        throw new InvalidOperationException("No se puede asignar un conductor con usuario inactivo.");
                }

                if (!vehicle.IsActive)
                    throw new InvalidOperationException("Cannot assign an inactive vehicle.");

                if (vehicle.Status != VehicleStatus.Available)
                    throw new InvalidOperationException("No se puede asignar un vehículo que no esté disponible.");

                var inProgressRoutes = await _uow.Routes.GetAllAsync(
                    driverId: driverId,
                    status: RouteStatus.InProgress,
                    onlyActive: true);
                if (inProgressRoutes.Count > 0)
                    throw new fletflow.Domain.Fleet.Exceptions.BusinessConflictException(
                        "No se puede reasignar un conductor con una ruta en progreso.");

                driver.AssignToVehicle(vehicle.Id);

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
