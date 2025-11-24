using System.Collections.Generic;
using System.Linq;

namespace fletflow.Domain.Fleet.Entities
{
    public class RouteE
    {
        public Guid Id { get; private set; }

        public Guid VehicleId { get; private set; }
        public Guid DriverId { get; private set; }
        public string Name { get; private set; } = default!;
        public RoutePoint Origin { get; private set; } = default!;
        public RoutePoint Destination { get; private set; } = default!;
        public IReadOnlyList<RoutePoint> Points => _points.AsReadOnly();
        public string? CargoDescription { get; private set; }

        public DateTime? PlannedStart { get; private set; }
        public DateTime? PlannedEnd { get; private set; }

        public RouteStatus Status { get; private set; }
        public bool IsActive { get; private set; }
        private readonly List<RoutePoint> _points = new();

        private RouteE() { }

        private RouteE(
            Guid id,
            Guid vehicleId,
            Guid driverId,
            string name,
            RoutePoint origin,
            RoutePoint destination,
            IEnumerable<RoutePoint> points,
            string? cargoDescription,
            DateTime? plannedStart,
            DateTime? plannedEnd,
            RouteStatus status,
            bool isActive)
        {
            Id = id;
            VehicleId = vehicleId;
            DriverId = driverId;
            Name = name;
            SetPointsInternal(origin, destination, points);
            CargoDescription = cargoDescription;
            PlannedStart = plannedStart;
            PlannedEnd = plannedEnd;
            Status = status;
            IsActive = isActive;
        }

        public static RouteE Create(
            Guid vehicleId,
            Guid driverId,
            string name,
            RoutePoint origin,
            RoutePoint destination,
            IEnumerable<RoutePoint>? points,
            string? cargoDescription,
            DateTime? plannedStart,
            DateTime? plannedEnd)
        {
            name = name.Trim();
            cargoDescription = string.IsNullOrWhiteSpace(cargoDescription) ? null : cargoDescription.Trim();
            var orderedPoints = BuildOrderedPoints(origin, destination, points);

            return new RouteE(
                Guid.NewGuid(),
                vehicleId,
                driverId,
                name,
                origin,
                destination,
                orderedPoints,
                cargoDescription,
                plannedStart,
                plannedEnd,
                RouteStatus.Pending,
                isActive: true
            );
        }

        public static RouteE CreateExisting(
            Guid id,
            Guid vehicleId,
            Guid driverId,
            string name,
            RoutePoint origin,
            RoutePoint destination,
            IEnumerable<RoutePoint> points,
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
                name,
                origin,
                destination,
                points,
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
            string name,
            RoutePoint origin,
            RoutePoint destination,
            IEnumerable<RoutePoint>? points,
            string? cargoDescription,
            DateTime? plannedStart,
            DateTime? plannedEnd)
        {
            VehicleId = vehicleId;
            DriverId = driverId;
            Name = name.Trim();
            var orderedPoints = BuildOrderedPoints(origin, destination, points);
            SetPointsInternal(origin, destination, orderedPoints);
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

        private static List<RoutePoint> BuildOrderedPoints(RoutePoint origin, RoutePoint destination, IEnumerable<RoutePoint>? points)
        {
            if (origin is null) throw new ArgumentNullException(nameof(origin));
            if (destination is null) throw new ArgumentNullException(nameof(destination));

            var list = new List<RoutePoint>();
            var provided = points?.ToList() ?? new List<RoutePoint>();

            if (provided.Count >= 2)
            {
                // Asumimos que ya vienen ordenados incluyendo origen/destino.
                return provided;
            }

            // Fallback: sólo origen/destino o waypoint suelto
            list.Add(origin);
            if (provided.Count == 1)
            {
                list.Add(provided[0]);
            }
            list.Add(destination);
            return list;
        }

        private void SetPointsInternal(RoutePoint origin, RoutePoint destination, IEnumerable<RoutePoint> points)
        {
            _points.Clear();
            _points.AddRange(points);

            // Garantiza que origin/destination coincidan con extremos de la lista
            Origin = _points.FirstOrDefault() ?? origin;
            Destination = _points.LastOrDefault() ?? destination;
        }
    }
}
