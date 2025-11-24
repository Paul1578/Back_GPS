namespace fletflow.Infrastructure.Persistence.Entities
{
    public class RouteEntity
    {
        public Guid Id { get; set; }

        public Guid VehicleId { get; set; }
        public VehicleEntity Vehicle { get; set; } = default!;

        public Guid DriverId { get; set; }
        public DriverEntity Driver { get; set; } = default!;
        public string Name { get; set; } = default!;

        public string Origin { get; set; } = default!;
        public double OriginLat { get; set; }
        public double OriginLng { get; set; }

        public string Destination { get; set; } = default!;
        public double DestinationLat { get; set; }
        public double DestinationLng { get; set; }
        public string? CargoDescription { get; set; }

        public DateTime? PlannedStart { get; set; }
        public DateTime? PlannedEnd { get; set; }

        public int Status { get; set; }  // se mapeará a RouteStatus en dominio
        public bool IsActive { get; set; }

        public string PointsJson { get; set; } = "[]";
    }
}
