namespace fletflow.Application.Fleet.Dtos
{
    public class RoutePositionDto
    {
        public Guid Id { get; set; }
        public Guid RouteId { get; set; }

        public double Latitude { get; set; }
        public double Longitude { get; set; }

        public DateTime RecordedAt { get; set; }
        public double? SpeedKmh { get; set; }
        public double? Heading { get; set; }
    }
}
