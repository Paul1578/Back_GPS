using fletflow.Application.Fleet.Dtos;
using fletflow.Domain.Fleet.Entities;

namespace fletflow.Application.Fleet.Mappings
{
    public static class VehicleApplicationMapper
    {
        public static VehicleDto ToDto(Vehicle vehicle)
        {
            return new VehicleDto
            {
                Id = vehicle.Id,
                Plate = vehicle.Plate,
                Brand = vehicle.Brand,
                Model = vehicle.Model,
                Year = vehicle.Year,
                Description = vehicle.Description,
                IsActive = vehicle.IsActive,
                Status = vehicle.Status
            };
        }

        public static List<VehicleDto> ToDtoList(IEnumerable<Vehicle> vehicles)
            => vehicles.Select(ToDto).ToList();
    }
}
