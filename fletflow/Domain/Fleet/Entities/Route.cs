namespace fletflow.Domain.Fleet.Entities
{
    public class RouteE
    {
        public Guid Id { get; private set; }

        public Guid VehicleId { get; private set; }
        public Guid DriverId { get; private set; }

        public string Origin { get; private set; } = default!;
        public string Destination { get; private set; } = default!;
        public string? CargoDescription { get; private set; }

        public DateTime? PlannedStart { get; private set; }
        public DateTime? PlannedEnd { get; private set; }

        public RouteStatus Status { get; private set; }
        public bool IsActive { get; private set; }

        private RouteE() { }

        private RouteE(
            Guid id,
            Guid vehicleId,
            Guid driverId,
            string origin,
            string destination,
            string? cargoDescription,
            DateTime? plannedStart,
            DateTime? plannedEnd,
            RouteStatus status,
            bool isActive)
        {
            Id = id;
            VehicleId = vehicleId;
            DriverId = driverId;
            Origin = origin;
            Destination = destination;
            CargoDescription = cargoDescription;
            PlannedStart = plannedStart;
            PlannedEnd = plannedEnd;
            Status = status;
            IsActive = isActive;
        }

        public static RouteE Create(
            Guid vehicleId,
            Guid driverId,
            string origin,
            string destination,
            string? cargoDescription,
            DateTime? plannedStart,
            DateTime? plannedEnd)
        {
            origin = origin.Trim();
            destination = destination.Trim();
            cargoDescription = string.IsNullOrWhiteSpace(cargoDescription) ? null : cargoDescription.Trim();

            return new RouteE(
                Guid.NewGuid(),
                vehicleId,
                driverId,
                origin,
                destination,
                cargoDescription,
                plannedStart,
                plannedEnd,
                RouteStatus.Planned,
                isActive: true
            );
        }

        public static RouteE CreateExisting(
            Guid id,
            Guid vehicleId,
            Guid driverId,
            string origin,
            string destination,
            string? cargoDescription,
            DateTime? plannedStart,
            DateTime? plannedEnd,
            RouteStatus status,
            bool isActive)
        {
            return new RouteE(
                id,
                vehicleId,
                driverId,
                origin,
                destination,
                cargoDescription,
                plannedStart,
                plannedEnd,
                status,
                isActive
            );
        }

        public void Update(
            Guid vehicleId,
            Guid driverId,
            string origin,
            string destination,
            string? cargoDescription,
            DateTime? plannedStart,
            DateTime? plannedEnd)
        {
            VehicleId = vehicleId;
            DriverId = driverId;
            Origin = origin.Trim();
            Destination = destination.Trim();
            CargoDescription = string.IsNullOrWhiteSpace(cargoDescription) ? null : cargoDescription.Trim();
            PlannedStart = plannedStart;
            PlannedEnd = plannedEnd;
        }

        public void ChangeStatus(RouteStatus newStatus)
        {
            // Aquí podrías meter reglas: no pasar de Completed a InProgress, etc.
            Status = newStatus;
        }

        public void Activate() => IsActive = true;
        public void Deactivate() => IsActive = false;
    }
}
