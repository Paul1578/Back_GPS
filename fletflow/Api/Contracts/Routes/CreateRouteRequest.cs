namespace fletflow.Api.Contracts.Routes
{
    public class CreateRouteRequest
    {
        public Guid VehicleId { get; set; }
        public Guid DriverId { get; set; }
        public string Origin { get; set; } = default!;
        public string Destination { get; set; } = default!;
        public string? CargoDescription { get; set; }
        public DateTime? PlannedStart { get; set; }
        public DateTime? PlannedEnd { get; set; }
    }
}
