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
                Origin = ToPointDto(route.Origin),
                Destination = ToPointDto(route.Destination),
                Points = route.Points.Select(ToPointDto).ToList(),
                CargoDescription = route.CargoDescription,
                PlannedStart = route.PlannedStart,
                PlannedEnd = route.PlannedEnd,
                Status = route.Status,
                IsActive = route.IsActive
            };
        }

        public static List<RouteDto> ToDtoList(IEnumerable<RouteE> routes)
            => routes.Select(ToDto).ToList();

        private static RoutePointDto ToPointDto(RoutePoint point)
        {
            return new RoutePointDto
            {
                Latitude = point.Latitude,
                Longitude = point.Longitude,
                Name = point.Name
            };
        }
    }
}
