using fletflow.Application.Fleet.Dtos;
using fletflow.Application.Fleet.Mappings;
using fletflow.Domain.Fleet.Entities;
using fletflow.Domain.Fleet.Repositories;
using System.Linq;

namespace fletflow.Application.Fleet.Queries
{
    public class GetRoutesQuery
    {
        private readonly IRouteRepository _routes;
        private readonly IVehicleRepository _vehicles;

        public GetRoutesQuery(IRouteRepository routes, IVehicleRepository vehicles)
        {
            _routes = routes;
            _vehicles = vehicles;
        }

        public async Task<List<RouteDto>> Execute(
            Guid? vehicleId = null,
            Guid? driverId = null,
            RouteStatus? status = null,
            bool? onlyActive = null)
        {
            var routes = await _routes.GetAllAsync(vehicleId, driverId, status, onlyActive);
            var routeDtos = RouteApplicationMapper.ToDtoList(routes);
            var vehicleIds = routeDtos.Select(route => route.VehicleId).Distinct().ToArray();
            var vehicles = await _vehicles.GetByIdsAsync(vehicleIds);
            var vehiclesById = vehicles.ToDictionary(vehicle => vehicle.Id);

            foreach (var route in routeDtos)
            {
                if (!vehiclesById.TryGetValue(route.VehicleId, out var vehicle))
                    continue;

                route.VehiclePlate = vehicle.Plate;
                route.VehicleBrand = vehicle.Brand;
                route.VehicleModel = vehicle.Model;
            }

            return routeDtos;
        }
    }
}
