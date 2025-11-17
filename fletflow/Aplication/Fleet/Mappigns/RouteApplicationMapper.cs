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
                Origin = route.Origin,
                Destination = route.Destination,
                CargoDescription = route.CargoDescription,
                PlannedStart = route.PlannedStart,
                PlannedEnd = route.PlannedEnd,
                Status = route.Status,
                IsActive = route.IsActive
            };
        }

        public static List<RouteDto> ToDtoList(IEnumerable<RouteE> routes)
            => routes.Select(ToDto).ToList();
    }
}
