namespace fletflow.Infrastructure.Persistence.Entities
{
    public class RoutePositionEntity
    {
        public Guid Id { get; set; }
        public Guid RouteId { get; set; }
        public RouteEntity Route { get; set; } = default!;

        public double Latitude { get; set; }
        public double Longitude { get; set; }

        public DateTime RecordedAt { get; set; }
        public double? SpeedKmh { get; set; }
        public double? Heading { get; set; }
    }
}
