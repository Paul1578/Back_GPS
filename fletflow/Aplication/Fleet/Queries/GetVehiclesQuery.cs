using fletflow.Application.Fleet.Dtos;
using fletflow.Application.Fleet.Mappings;
using fletflow.Domain.Fleet.Repositories;

namespace fletflow.Application.Fleet.Queries
{
    public class GetVehiclesQuery
    {
        private readonly IVehicleRepository _vehicles;

        public GetVehiclesQuery(IVehicleRepository vehicles)
        {
            _vehicles = vehicles;
        }

        public async Task<List<VehicleDto>> Execute(bool? onlyActive = null)
        {
            var vehicles = await _vehicles.GetAllAsync(onlyActive);
            return VehicleApplicationMapper.ToDtoList(vehicles);
        }
    }
}
