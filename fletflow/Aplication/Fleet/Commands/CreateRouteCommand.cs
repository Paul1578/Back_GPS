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
    public class CreateRouteCommand
    {
        private readonly IUnitOfWork _uow;
        private readonly IRouteRepository _routes;
        private readonly IVehicleRepository _vehicles;
        private readonly IDriverRepository _drivers;

        public CreateRouteCommand(
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
            Guid vehicleId,
            Guid driverId,
            string name,
            IEnumerable<RoutePoint> points,
            string? cargoDescription,
            DateTime? plannedStart,
            DateTime? plannedEnd)
        {
            var vehicle = await _vehicles.GetByIdAsync(vehicleId)
                ?? throw new KeyNotFoundException("Vehicle not found.");

            var driver = await _drivers.GetByIdAsync(driverId)
                ?? throw new KeyNotFoundException("Driver not found.");

            if (!vehicle.IsActive)
                throw new InvalidOperationException("Cannot create a route with an inactive vehicle.");

            if (vehicle.Status != VehicleStatus.Available)
                throw new InvalidOperationException("El vehículo debe estar disponible para asignarlo a una ruta.");

            if (!driver.IsActive)
                throw new InvalidOperationException("Cannot create a route with an inactive driver.");

            if (driver.UserId.HasValue)
            {
                var linkedUser = await _uow.Users.GetByIdAsync(driver.UserId.Value);
                if (linkedUser is null || !linkedUser.IsActive)
                    throw new InvalidOperationException("No se puede crear una ruta con un conductor cuyo usuario está inactivo.");
            }

            if (plannedStart.HasValue && plannedEnd.HasValue && plannedEnd <= plannedStart)
                throw new ArgumentException("La fecha final debe ser posterior a la fecha inicial.", nameof(plannedEnd));

            var pointsList = points?.ToList() ?? new List<RoutePoint>();
            if (pointsList.Count < 2)
                throw new ArgumentException("La ruta debe tener al menos origen y destino.", nameof(points));

            try
            {
                await _uow.LockRouteAssignmentAsync(vehicleId, driverId);
                // Reload parent state under the row locks, then check the current schedule.
                vehicle = await _vehicles.GetByIdAsync(vehicleId)
                    ?? throw new KeyNotFoundException("Vehicle not found.");
                driver = await _drivers.GetByIdAsync(driverId)
                    ?? throw new KeyNotFoundException("Driver not found.");

                if (!vehicle.IsActive || vehicle.Status != VehicleStatus.Available)
                    throw new BusinessConflictException("El vehículo ya no está disponible para asignarlo a una ruta.");
                if (!driver.IsActive)
                    throw new BusinessConflictException("El conductor ya no está disponible para asignarlo a una ruta.");

                var assignedDriver = await _drivers.GetOtherAssignedToVehicleAsync(vehicleId, driverId);
                if (assignedDriver is not null)
                    throw new BusinessConflictException(
                        "El vehículo ya está asignado a otro conductor. Desasígnalo antes de crear la ruta.");

                var activeVehicleRoutes = await _routes.GetAllAsync(vehicleId: vehicleId, onlyActive: true);
                var activeDriverRoutes = await _routes.GetAllAsync(driverId: driverId, onlyActive: true);
                if (HasScheduleConflict(activeVehicleRoutes, plannedStart, plannedEnd) ||
                    HasScheduleConflict(activeDriverRoutes, plannedStart, plannedEnd))
                    throw new BusinessConflictException("El vehículo o conductor ya tiene una ruta activa en ese horario.");

                var route = RouteE.Create(
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

                // La asignación de vehículo y la creación de la ruta comparten el mismo commit.
                if (driver.VehicleId != vehicleId)
                {
                    driver.AssignToVehicle(vehicleId);
                    await _drivers.UpdateAsync(driver);
                }

                await _routes.AddAsync(route);
                await _uow.CommitAsync();

                return RouteApplicationMapper.ToDto(route);
            }
            catch
            {
                _uow.Rollback();
                throw;
            }
        }

        private static bool HasScheduleConflict(IEnumerable<RouteE> routes, DateTime? start, DateTime? end)
        {
            return routes.Any(existing =>
                existing.Status is RouteStatus.Pending or RouteStatus.InProgress &&
                (!start.HasValue || !end.HasValue || !existing.PlannedStart.HasValue || !existing.PlannedEnd.HasValue ||
                 start.Value < existing.PlannedEnd.Value && existing.PlannedStart.Value < end.Value));
        }
    }
}
