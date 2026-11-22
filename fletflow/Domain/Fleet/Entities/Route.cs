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
        public DateTime? StartedAt { get; private set; }
        public DateTime? EndedAt { get; private set; }

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
            bool isActive,
            DateTime? startedAt = null,
            DateTime? endedAt = null)
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
            StartedAt = startedAt;
            EndedAt = endedAt;
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
            bool isActive,
            DateTime? startedAt = null,
            DateTime? endedAt = null)
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
                isActive,
                startedAt,
                endedAt
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
            if (newStatus == RouteStatus.Completed && !AllOperationalStopsComplete())
                throw new InvalidOperationException("Completa cada parada operativa y registra evidencia antes de cerrar la ruta.");
            if (newStatus == RouteStatus.InProgress && StartedAt is null)
                StartedAt = DateTime.UtcNow;
            if (newStatus is RouteStatus.Completed or RouteStatus.Cancelled)
                EndedAt = DateTime.UtcNow;
            Status = newStatus;
        }

        public void UpdatePointStatus(int pointIndex, RoutePointStatus newStatus)
        {
            if (Status != RouteStatus.InProgress)
                throw new InvalidOperationException("Las paradas sólo se pueden actualizar con la ruta en progreso.");
            if (pointIndex < 0 || pointIndex >= _points.Count)
                throw new ArgumentOutOfRangeException(nameof(pointIndex), "La parada no existe.");
            if (_points[pointIndex].Operation == RoutePointOperation.None)
                throw new InvalidOperationException("El origen y el destino no admiten estado de entrega/recogida.");
            if (!IsAllowedPointTransition(_points[pointIndex].Status, newStatus))
                throw new InvalidOperationException("La transición de estado de la parada no es válida.");

            var updatedPoints = _points.ToList();
            updatedPoints[pointIndex] = updatedPoints[pointIndex].WithStatus(newStatus);
            SetPointsInternal(updatedPoints[0], updatedPoints[^1], updatedPoints);
        }

        public void AddPointEvidence(int pointIndex, RoutePointEvidence evidence)
        {
            if (Status != RouteStatus.InProgress)
                throw new InvalidOperationException("Sólo se puede adjuntar evidencia con la ruta en progreso.");
            if (pointIndex < 0 || pointIndex >= _points.Count)
                throw new ArgumentOutOfRangeException(nameof(pointIndex), "La parada no existe.");
            if (_points[pointIndex].Operation == RoutePointOperation.None)
                throw new InvalidOperationException("Sólo se puede adjuntar evidencia a una parada operativa.");
            if (_points[pointIndex].Status is RoutePointStatus.Completed or RoutePointStatus.Skipped)
                throw new InvalidOperationException("No se puede adjuntar evidencia a una parada cerrada.");

            var updatedPoints = _points.ToList();
            updatedPoints[pointIndex] = updatedPoints[pointIndex].WithEvidence(evidence);
            SetPointsInternal(updatedPoints[0], updatedPoints[^1], updatedPoints);
        }

        private bool AllOperationalStopsComplete()
            => _points.Where(point => point.Operation != RoutePointOperation.None)
                .All(point => (point.Status is RoutePointStatus.Completed or RoutePointStatus.Skipped)
                              && point.Evidence.Count > 0);

        private static bool IsAllowedPointTransition(RoutePointStatus current, RoutePointStatus next)
            => (current, next) switch
            {
                (RoutePointStatus.Pending, RoutePointStatus.InProgress) => true,
                (RoutePointStatus.Pending, RoutePointStatus.Skipped) => true,
                (RoutePointStatus.InProgress, RoutePointStatus.Completed) => true,
                (RoutePointStatus.InProgress, RoutePointStatus.Failed) => true,
                (RoutePointStatus.Failed, RoutePointStatus.InProgress) => true,
                _ => false
            };

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
