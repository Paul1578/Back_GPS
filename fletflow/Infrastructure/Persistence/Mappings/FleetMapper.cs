using fletflow.Domain.Fleet.Entities;
using fletflow.Infrastructure.Persistence.Entities;

namespace fletflow.Infrastructure.Persistence.Mappings
{
    public static class FleetMapper
    {
        public static Vehicle ToDomain(VehicleEntity entity)
        {
            return Vehicle.CreateExisting(
                entity.Id,
                entity.Plate,
                entity.Brand,
                entity.Model,
                entity.Year,
                entity.Description,
                entity.IsActive
            );
        }

        public static VehicleEntity ToEntity(Vehicle domain)
        {
            return new VehicleEntity
            {
                Id = domain.Id,
                Plate = domain.Plate,
                Brand = domain.Brand,
                Model = domain.Model,
                Year = domain.Year,
                Description = domain.Description,
                IsActive = domain.IsActive
            };
        }
    }
}
