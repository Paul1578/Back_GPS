using fletflow.Domain.Fleet.Entities;
using fletflow.Domain.Fleet.Exceptions;
using fletflow.Domain.Fleet.Repositories;
using fletflow.Infrastructure.Persistence.Contracts;

namespace fletflow.Application.Fleet.Commands;

public class ReactivateVehicleCommand
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IVehicleRepository _vehicles;

    public ReactivateVehicleCommand(IUnitOfWork unitOfWork, IVehicleRepository vehicles)
    {
        _unitOfWork = unitOfWork;
        _vehicles = vehicles;
    }

    public async Task Execute(Guid id)
    {
        try
        {
            await _unitOfWork.LockRouteAssignmentAsync(id, null);
            var vehicle = await _vehicles.GetByIdAsync(id)
                ?? throw new KeyNotFoundException("Vehicle not found.");
            if (vehicle.IsActive)
            {
                _unitOfWork.Rollback();
                return;
            }

            var inProgressRoutes = await _unitOfWork.Routes.GetAllAsync(
                vehicleId: id,
                status: RouteStatus.InProgress,
                onlyActive: true);
            if (inProgressRoutes.Count > 0)
                throw new BusinessConflictException("No se puede reactivar un vehículo con una ruta en progreso.");

            if (vehicle.Status == VehicleStatus.OnRoute)
                vehicle.SetStatus(VehicleStatus.Available);
            vehicle.Activate();

            await _vehicles.UpdateAsync(vehicle);
            await _unitOfWork.CommitAsync();
        }
        catch
        {
            _unitOfWork.Rollback();
            throw;
        }
    }
}
