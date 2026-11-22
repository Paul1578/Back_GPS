using fletflow.Domain.Fleet.Entities;
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

        public async Task Execute(Guid id, VehicleStatus status)
        {
            if (!Enum.IsDefined(typeof(VehicleStatus), status))
                throw new ArgumentException("Estado de vehículo no válido.", nameof(status));

            try
            {
                await _uow.LockRouteAssignmentAsync(id, null);
                var vehicle = await _vehicles.GetByIdAsync(id)
                    ?? throw new KeyNotFoundException("Vehicle not found.");
                var inProgressRoutes = await _uow.Routes.GetAllAsync(
                    vehicleId: id,
                    status: RouteStatus.InProgress,
                    onlyActive: true);

                if (inProgressRoutes.Count > 0 && status != VehicleStatus.OnRoute)
                    throw new fletflow.Domain.Fleet.Exceptions.BusinessConflictException(
                        "No se puede cambiar el estado de un vehículo con una ruta en progreso.");
                if (status == VehicleStatus.OnRoute && inProgressRoutes.Count == 0)
                    throw new fletflow.Domain.Fleet.Exceptions.BusinessConflictException(
                        "El estado En ruta sólo se asigna al iniciar una ruta.");

                vehicle.SetStatus(status);

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
