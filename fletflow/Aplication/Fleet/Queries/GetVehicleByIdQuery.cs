using fletflow.Application.Fleet.Dtos;
using fletflow.Application.Fleet.Mappings;
using fletflow.Domain.Fleet.Repositories;

namespace fletflow.Application.Fleet.Queries
{
    public class GetVehicleByIdQuery
    {
        private readonly IVehicleRepository _vehicles;

        public GetVehicleByIdQuery(IVehicleRepository vehicles)
        {
            _vehicles = vehicles;
        }

        public async Task<VehicleDto> Execute(Guid id)
        {
            var vehicle = await _vehicles.GetByIdAsync(id)
                ?? throw new KeyNotFoundException("Vehicle not found.");

            return VehicleApplicationMapper.ToDto(vehicle);
        }
    }
}
