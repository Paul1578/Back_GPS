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
                entity.IsActive,
                (VehicleStatus)entity.Status
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
                IsActive = domain.IsActive,
                Status = (int)domain.Status
            };
        }

        public static Driver ToDomain(DriverEntity entity)
        {
            return Driver.CreateExisting(
                entity.Id,
                entity.FirstName,
                entity.LastName,
                entity.DocumentNumber,
                entity.PhoneNumber,
                entity.IsActive,
                entity.VehicleId
            );
        }

        public static DriverEntity ToEntity(Driver domain)
        {
            return new DriverEntity
            {
                Id = domain.Id,
                FirstName = domain.FirstName,
                LastName = domain.LastName,
                DocumentNumber = domain.DocumentNumber,
                PhoneNumber = domain.PhoneNumber,
                IsActive = domain.IsActive,
                VehicleId = domain.VehicleId
            };
        }
        public static RouteE ToDomain(RouteEntity entity)
        {
            return RouteE.CreateExisting(
                entity.Id,
                entity.VehicleId,
                entity.DriverId,
                entity.Origin,
                entity.Destination,
                entity.CargoDescription,
                entity.PlannedStart,
                entity.PlannedEnd,
                (RouteStatus)entity.Status,
                entity.IsActive
            );
        }

        public static RouteEntity ToEntity(RouteE domain)
        {
            return new RouteEntity
            {
                Id = domain.Id,
                VehicleId = domain.VehicleId,
                DriverId = domain.DriverId,
                Origin = domain.Origin,
                Destination = domain.Destination,
                CargoDescription = domain.CargoDescription,
                PlannedStart = domain.PlannedStart,
                PlannedEnd = domain.PlannedEnd,
                Status = (int)domain.Status,
                IsActive = domain.IsActive
            };
        }

        public static RoutePosition ToDomain(RoutePositionEntity entity)
        {
            return RoutePosition.CreateExisting(
                entity.Id,
                entity.RouteId,
                entity.Latitude,
                entity.Longitude,
                entity.RecordedAt,
                entity.SpeedKmh,
                entity.Heading
            );
        }

        public static RoutePositionEntity ToEntity(RoutePosition domain)
        {
            return new RoutePositionEntity
            {
                Id = domain.Id,
                RouteId = domain.RouteId,
                Latitude = domain.Latitude,
                Longitude = domain.Longitude,
                RecordedAt = domain.RecordedAt,
                SpeedKmh = domain.SpeedKmh,
                Heading = domain.Heading
            };
        }

    }
}
