using System.Collections.Generic;

namespace fletflow.Api.Contracts.Routes
{
    public class CreateRouteRequest
    {
        public Guid VehicleId { get; set; }
        public Guid DriverId { get; set; }
        public string Name { get; set; } = default!;
        public List<RoutePointRequest> Points { get; set; } = new();
        public RoutePointRequest? Origin { get; set; }
        public RoutePointRequest? Destination { get; set; }
        public string? CargoDescription { get; set; }
        public DateTime? PlannedStart { get; set; }
        public DateTime? PlannedEnd { get; set; }
    }
}
