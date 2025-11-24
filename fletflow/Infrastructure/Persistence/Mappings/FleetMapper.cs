using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
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
                entity.VehicleId,
                entity.UserId
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
                VehicleId = domain.VehicleId,
                UserId = domain.UserId
            };
        }
        public static RouteE ToDomain(RouteEntity entity)
        {
            var points = DeserializePoints(entity);

            return RouteE.CreateExisting(
                entity.Id,
                entity.VehicleId,
                entity.DriverId,
                entity.Name,
                points.First(),
                points.Last(),
                points,
                entity.CargoDescription,
                entity.PlannedStart,
                entity.PlannedEnd,
                (RouteStatus)entity.Status,
                entity.IsActive
            );
        }

        public static RouteEntity ToEntity(RouteE domain)
        {
            var origin = domain.Origin;
            var destination = domain.Destination;
            var pointsJson = JsonSerializer.Serialize(domain.Points);

            return new RouteEntity
            {
                Id = domain.Id,
                VehicleId = domain.VehicleId,
                DriverId = domain.DriverId,
                Name = domain.Name,
                Origin = origin.Name ?? domain.Name,
                OriginLat = origin.Latitude,
                OriginLng = origin.Longitude,
                Destination = destination.Name ?? domain.Name,
                DestinationLat = destination.Latitude,
                DestinationLng = destination.Longitude,
                CargoDescription = domain.CargoDescription,
                PlannedStart = domain.PlannedStart,
                PlannedEnd = domain.PlannedEnd,
                Status = (int)domain.Status,
                IsActive = domain.IsActive,
                PointsJson = pointsJson
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

        private static List<RoutePoint> DeserializePoints(RouteEntity entity)
        {
            try
            {
                var deserialized = JsonSerializer.Deserialize<List<SerializableRoutePoint>>(entity.PointsJson);
                if (deserialized is { Count: > 0 })
                {
                    return deserialized
                        .Select(p => RoutePoint.Create(p.Latitude, p.Longitude, p.Name))
                        .ToList();
                }
            }
            catch
            {
                // ignored -> fallback below
            }

            // Fallback a origen/destino básicos
            var points = new List<RoutePoint>();

            points.Add(RoutePoint.Create(entity.OriginLat, entity.OriginLng, entity.Origin));
            points.Add(RoutePoint.Create(entity.DestinationLat, entity.DestinationLng, entity.Destination));

            return points;
        }

        private record SerializableRoutePoint(double Latitude, double Longitude, string? Name);
    }
}
