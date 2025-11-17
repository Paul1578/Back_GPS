using fletflow.Domain.Fleet.Repositories;
using fletflow.Infrastructure.Persistence.Contracts;

namespace fletflow.Application.Fleet.Commands
{
    public class ChangeVehicleStatusCommand
    {
        private readonly IUnitOfWork _uow;
        private readonly IVehicleRepository _vehicles;

        public ChangeVehicleStatusCommand(IUnitOfWork uow, IVehicleRepository vehicles)
        {
            _uow = uow;
            _vehicles = vehicles;
        }

        public async Task Execute(Guid id, bool isActive)
        {
            var vehicle = await _vehicles.GetByIdAsync(id)
                ?? throw new KeyNotFoundException("Vehículo no encontrado.");

            if (isActive)
                vehicle.Activate();
            else
                vehicle.Deactivate();

            await _vehicles.UpdateAsync(vehicle);
            await _uow.CommitAsync();
        }
    }
}
