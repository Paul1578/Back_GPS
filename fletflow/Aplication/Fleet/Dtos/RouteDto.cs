using fletflow.Domain.Fleet.Entities;

namespace fletflow.Application.Fleet.Dtos
{
    public class RouteDto
    {
        public Guid Id { get; set; }

        public Guid VehicleId { get; set; }
        public Guid DriverId { get; set; }

        public string Origin { get; set; } = default!;
        public string Destination { get; set; } = default!;
        public string? CargoDescription { get; set; }

        public DateTime? PlannedStart { get; set; }
        public DateTime? PlannedEnd { get; set; }

        public RouteStatus Status { get; set; }
        public bool IsActive { get; set; }
    }
}
