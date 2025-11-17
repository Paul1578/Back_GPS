namespace fletflow.Api.Contracts.Routes
{
    public class RegisterRoutePositionRequest
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }

        // Opcionales
        public DateTime? RecordedAt { get; set; }
        public double? SpeedKmh { get; set; }
        public double? Heading { get; set; }
    }
}
