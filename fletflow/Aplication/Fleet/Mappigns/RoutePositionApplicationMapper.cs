using fletflow.Application.Fleet.Dtos;
using fletflow.Domain.Fleet.Entities;

namespace fletflow.Application.Fleet.Mappings
{
    public static class RoutePositionApplicationMapper
    {
        public static RoutePositionDto ToDto(RoutePosition position)
        {
            return new RoutePositionDto
            {
                Id = position.Id,
                RouteId = position.RouteId,
                Latitude = position.Latitude,
                Longitude = position.Longitude,
                RecordedAt = position.RecordedAt,
                SpeedKmh = position.SpeedKmh,
                Heading = position.Heading
            };
        }

        public static List<RoutePositionDto> ToDtoList(IEnumerable<RoutePosition> positions)
            => positions.Select(ToDto).ToList();
    }
}
