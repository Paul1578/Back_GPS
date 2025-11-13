using fletflow.Domain.Fleet.Repositories;
using fletflow.Infrastructure.Persistence.Contracts;

namespace fletflow.Application.Fleet.Commands
{
    public class UnassignDriverFromVehicleCommand
    {
        private readonly IUnitOfWork _uow;
        private readonly IDriverRepository _drivers;

        public UnassignDriverFromVehicleCommand(
            IUnitOfWork uow,
            IDriverRepository drivers)
        {
            _uow = uow;
            _drivers = drivers;
        }

        public async Task Execute(Guid driverId)
        {
            var driver = await _drivers.GetByIdAsync(driverId)
                ?? throw new KeyNotFoundException("Conductor no encontrado.");

            driver.UnassignVehicle();

            await _drivers.UpdateAsync(driver);
            await _uow.CommitAsync();
        }
    }
}
