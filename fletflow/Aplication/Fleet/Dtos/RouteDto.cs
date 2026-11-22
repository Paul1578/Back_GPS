using System.Collections.Generic;
using fletflow.Domain.Fleet.Entities;

namespace fletflow.Application.Fleet.Dtos
{
    public class RouteDto
    {
        public Guid Id { get; set; }

        public Guid VehicleId { get; set; }
        public string? VehiclePlate { get; set; }
        public string? VehicleBrand { get; set; }
        public string? VehicleModel { get; set; }
        public Guid DriverId { get; set; }
        public string Name { get; set; } = default!;
        public RoutePointDto Origin { get; set; } = default!;
        public RoutePointDto Destination { get; set; } = default!;
        public List<RoutePointDto> Points { get; set; } = new();
        public string? CargoDescription { get; set; }

        public DateTime? PlannedStart { get; set; }
        public DateTime? PlannedEnd { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? EndedAt { get; set; }
        public DateTime? CreatedAtUtc { get; set; }

        public RouteStatus Status { get; set; }
        public bool IsActive { get; set; }
    }
}
