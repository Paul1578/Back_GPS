using System.Linq;
using System.Collections.Generic;
using fletflow.Application.Fleet.Dtos;
using fletflow.Application.Fleet.Mappings;
using fletflow.Domain.Fleet.Entities;
using fletflow.Domain.Fleet.Exceptions;
using fletflow.Domain.Fleet.Repositories;
using fletflow.Infrastructure.Persistence.Contracts;

namespace fletflow.Application.Fleet.Commands
{
    public class UpdateRouteCommand
    {
        private readonly IUnitOfWork _uow;
        private readonly IRouteRepository _routes;
        private readonly IVehicleRepository _vehicles;
        private readonly IDriverRepository _drivers;

        public UpdateRouteCommand(
            IUnitOfWork uow,
            IRouteRepository routes,
            IVehicleRepository vehicles,
            IDriverRepository drivers)
        {
            _uow = uow;
            _routes = routes;
            _vehicles = vehicles;
            _drivers = drivers;
        }

        public async Task<RouteDto> Execute(
            Guid id,
            Guid vehicleId,
            Guid driverId,
            string name,
            IEnumerable<RoutePoint> points,
            string? cargoDescription,
            DateTime? plannedStart,
            DateTime? plannedEnd)
        {
            var route = await _routes.GetByIdAsync(id)
                ?? throw new KeyNotFoundException("Route not found.");

            if (route.Status != RouteStatus.Pending)
                throw new InvalidOperationException("Sólo se pueden editar rutas pendientes.");

            var vehicle = await _vehicles.GetByIdAsync(vehicleId)
                ?? throw new KeyNotFoundException("Vehicle not found.");

            var driver = await _drivers.GetByIdAsync(driverId)
                ?? throw new KeyNotFoundException("Driver not found.");

            if (!vehicle.IsActive)
                throw new InvalidOperationException("Cannot assign an inactive vehicle.");

            if (vehicle.Status != VehicleStatus.Available && vehicleId != route.VehicleId)
                throw new InvalidOperationException("El vehículo debe estar disponible para asignarlo a una ruta.");

            if (!driver.IsActive)
                throw new InvalidOperationException("Cannot assign an inactive driver.");

            if (plannedStart.HasValue && plannedEnd.HasValue && plannedEnd <= plannedStart)
                throw new ArgumentException("La fecha final debe ser posterior a la fecha inicial.", nameof(plannedEnd));

            var pointsList = points?.ToList() ?? new List<RoutePoint>();
            if (pointsList.Count < 2)
                throw new ArgumentException("La ruta debe tener al menos origen y destino.", nameof(points));

            try
            {
                await _uow.LockRouteAssignmentAsync(vehicleId, driverId, id);
                // Reload route and parent state under the locks before checking availability.
                route = await _routes.GetByIdAsync(id)
                    ?? throw new KeyNotFoundException("Route not found.");
                if (route.Status != RouteStatus.Pending)
                    throw new BusinessConflictException("La ruta dejó de estar pendiente y no se puede editar.");

                vehicle = await _vehicles.GetByIdAsync(vehicleId)
                    ?? throw new KeyNotFoundException("Vehicle not found.");
                driver = await _drivers.GetByIdAsync(driverId)
                    ?? throw new KeyNotFoundException("Driver not found.");

                if (!vehicle.IsActive || (vehicle.Status != VehicleStatus.Available && vehicleId != route.VehicleId))
                    throw new BusinessConflictException("El vehículo ya no está disponible para asignarlo a una ruta.");
                if (!driver.IsActive)
                    throw new BusinessConflictException("El conductor ya no está disponible para asignarlo a una ruta.");
                if (driver.UserId.HasValue)
                {
                    await _uow.LockLinkedUserForRouteAssignmentAsync(driver.UserId.Value);
                    var linkedUser = await _uow.Users.GetByIdAsync(driver.UserId.Value);
                    if (linkedUser?.IsActive != true)
                        throw new BusinessConflictException("La cuenta del conductor ya no está disponible para asignarlo a una ruta.");
                }

                var assignedDriver = await _drivers.GetOtherAssignedToVehicleAsync(vehicleId, driverId);
                if (assignedDriver is not null)
                    throw new BusinessConflictException(
                        "El vehículo ya está asignado a otro conductor. Desasígnalo antes de editar la ruta.");

                var vehicleRoutes = await _routes.GetAllAsync(vehicleId: vehicleId, onlyActive: true);
                var driverRoutes = await _routes.GetAllAsync(driverId: driverId, onlyActive: true);
                if (HasScheduleConflict(vehicleRoutes, id, plannedStart, plannedEnd) ||
                    HasScheduleConflict(driverRoutes, id, plannedStart, plannedEnd))
                    throw new BusinessConflictException("El vehículo o conductor ya tiene una ruta activa en ese horario.");

                route.Update(
                    vehicleId,
                    driverId,
                    name,
                    pointsList.First(),
                    pointsList.Last(),
                    pointsList,
                    cargoDescription,
                    plannedStart,
                    plannedEnd
                );

                if (driver.VehicleId != vehicleId)
                {
                    driver.AssignToVehicle(vehicleId);
                    await _drivers.UpdateAsync(driver);
                }

                await _routes.UpdateAsync(route);
                await _uow.CommitAsync();

                return RouteApplicationMapper.ToDto(route);
            }
            catch
            {
                _uow.Rollback();
                throw;
            }
        }

        private static bool HasScheduleConflict(IEnumerable<RouteE> routes, Guid excludedRouteId, DateTime? start, DateTime? end)
        {
            return routes.Any(existing => existing.Id != excludedRouteId &&
                existing.Status is RouteStatus.Pending or RouteStatus.InProgress &&
                (!start.HasValue || !end.HasValue || !existing.PlannedStart.HasValue || !existing.PlannedEnd.HasValue ||
                 start.Value < existing.PlannedEnd.Value && existing.PlannedStart.Value < end.Value));
        }
    }
}
