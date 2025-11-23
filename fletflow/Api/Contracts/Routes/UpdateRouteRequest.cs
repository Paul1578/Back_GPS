namespace fletflow.Api.Contracts.Routes
{
    public class UpdateRouteRequest
    {
        public Guid VehicleId { get; set; }
        public Guid DriverId { get; set; }
        public string Name { get; set; } = default!;
        public string Origin { get; set; } = default!;
        public string Destination { get; set; } = default!;
        public string? CargoDescription { get; set; }
        public DateTime? PlannedStart { get; set; }
        public DateTime? PlannedEnd { get; set; }
    }
}
