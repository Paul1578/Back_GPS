using fletflow.Application.Fleet.Dtos;
using fletflow.Application.Fleet.Mappings;
using fletflow.Domain.Fleet.Repositories;

namespace fletflow.Application.Fleet.Queries
{
    public class GetRouteByIdQuery
    {
        private readonly IRouteRepository _routes;
        private readonly IVehicleRepository _vehicles;

        public GetRouteByIdQuery(IRouteRepository routes, IVehicleRepository vehicles)
        {
            _routes = routes;
            _vehicles = vehicles;
        }

        public async Task<RouteDto> Execute(Guid id)
        {
            var route = await _routes.GetByIdAsync(id)
                ?? throw new KeyNotFoundException("Route not found.");

            var dto = RouteApplicationMapper.ToDto(route);
            var vehicle = await _vehicles.GetByIdAsync(route.VehicleId);
            if (vehicle is not null)
            {
                dto.VehiclePlate = vehicle.Plate;
                dto.VehicleBrand = vehicle.Brand;
                dto.VehicleModel = vehicle.Model;
            }

            return dto;
        }
    }
}
