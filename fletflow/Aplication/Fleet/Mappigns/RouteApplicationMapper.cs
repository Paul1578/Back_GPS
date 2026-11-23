using System.Linq;
using fletflow.Application.Fleet.Dtos;
using fletflow.Domain.Fleet.Entities;

namespace fletflow.Application.Fleet.Mappings
{
    public static class RouteApplicationMapper
    {
        public static RouteDto ToDto(RouteE route)
        {
            return new RouteDto
            {
                Id = route.Id,
                VehicleId = route.VehicleId,
                DriverId = route.DriverId,
                Name = route.Name,
                Origin = ToPointDto(route.Origin, includeImageContent: true),
                Destination = ToPointDto(route.Destination, includeImageContent: true),
                Points = route.Points.Select(point => ToPointDto(point, includeImageContent: true)).ToList(),
                CargoDescription = route.CargoDescription,
                PlannedStart = route.PlannedStart,
                PlannedEnd = route.PlannedEnd,
                StartedAt = route.StartedAt,
                EndedAt = route.EndedAt,
                CreatedAtUtc = route.CreatedAtUtc,
                Status = route.Status,
                IsActive = route.IsActive
            };
        }

        public static List<RouteDto> ToDtoList(IEnumerable<RouteE> routes)
            => routes.Select(route => ToDtoListItem(route)).ToList();

        private static RouteDto ToDtoListItem(RouteE route)
        {
            return new RouteDto
            {
                Id = route.Id,
                VehicleId = route.VehicleId,
                DriverId = route.DriverId,
                Name = route.Name,
                Origin = ToPointDto(route.Origin, includeImageContent: false),
                Destination = ToPointDto(route.Destination, includeImageContent: false),
                Points = route.Points.Select(point => ToPointDto(point, includeImageContent: false)).ToList(),
                CargoDescription = route.CargoDescription,
                PlannedStart = route.PlannedStart,
                PlannedEnd = route.PlannedEnd,
                StartedAt = route.StartedAt,
                EndedAt = route.EndedAt,
                CreatedAtUtc = route.CreatedAtUtc,
                Status = route.Status,
                IsActive = route.IsActive
            };
        }

        private static RoutePointDto ToPointDto(RoutePoint point, bool includeImageContent)
        {
            return new RoutePointDto
            {
                Latitude = point.Latitude,
                Longitude = point.Longitude,
                Name = point.Name,
                Operation = point.Operation,
                Status = point.Status,
                Evidence = point.Evidence.Select(evidence => new RoutePointEvidenceDto
                {
                    Type = evidence.Type,
                    Content = includeImageContent || evidence.Type != "image" ? evidence.Content : null,
                    Description = evidence.Description,
                    CreatedAtUtc = evidence.CreatedAtUtc
                }).ToList()
            };
        }
    }
}
