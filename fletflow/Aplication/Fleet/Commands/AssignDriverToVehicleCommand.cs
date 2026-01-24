using fletflow.Domain.Fleet.Repositories;
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
            var driver = await _drivers.GetByIdAsync(driverId)
                ?? throw new KeyNotFoundException("Driver not found.");

            var vehicle = await _vehicles.GetByIdAsync(vehicleId)
                ?? throw new KeyNotFoundException("Vehicle not found.");

            if (!driver.IsActive)
                throw new InvalidOperationException("Cannot assign an inactive driver.");

            if (!vehicle.IsActive)
                throw new InvalidOperationException("Cannot assign an inactive vehicle.");

            // Regla simple de MVP:
            // si el driver ya tiene vehículo, lo pisamos
            driver.AssignToVehicle(vehicle.Id);

            await _drivers.UpdateAsync(driver);
            await _uow.CommitAsync();
        }
    }
}
