using System.Linq;
using System.Collections.Generic;
using fletflow.Application.Fleet.Dtos;
using fletflow.Application.Fleet.Mappings;
using fletflow.Domain.Fleet.Entities;
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
                ?? throw new KeyNotFoundException("Ruta no encontrada.");

            var vehicle = await _vehicles.GetByIdAsync(vehicleId)
                ?? throw new KeyNotFoundException("Vehículo no encontrado.");

            var driver = await _drivers.GetByIdAsync(driverId)
                ?? throw new KeyNotFoundException("Conductor no encontrado.");

            if (!vehicle.IsActive)
                throw new InvalidOperationException("No se puede asignar un vehículo inactivo.");

            if (!driver.IsActive)
                throw new InvalidOperationException("No se puede asignar un conductor inactivo.");

            var pointsList = points?.ToList() ?? new List<RoutePoint>();
            if (pointsList.Count < 2)
                throw new ArgumentException("La ruta debe tener al menos origen y destino.", nameof(points));

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

            await _routes.UpdateAsync(route);
            await _uow.CommitAsync();

            return RouteApplicationMapper.ToDto(route);
        }
    }
}
