namespace fletflow.Domain.Fleet.Entities
{
    public class RoutePoint
    {
        public double Latitude { get; }
        public double Longitude { get; }
        public string? Name { get; }
        public RoutePointOperation Operation { get; }
        public RoutePointStatus Status { get; }
        public IReadOnlyList<RoutePointEvidence> Evidence { get; }

        private RoutePoint(
            double latitude,
            double longitude,
            string? name,
            RoutePointOperation operation,
            RoutePointStatus status,
            IEnumerable<RoutePointEvidence>? evidence)
        {
            Latitude = latitude;
            Longitude = longitude;
            Name = string.IsNullOrWhiteSpace(name) ? null : name.Trim();
            Operation = operation;
            Status = status;
            Evidence = evidence?.ToList() ?? new List<RoutePointEvidence>();
        }

        public static RoutePoint Create(
            double latitude,
            double longitude,
            string? name = null,
            RoutePointOperation operation = RoutePointOperation.None,
            RoutePointStatus status = RoutePointStatus.Pending,
            IEnumerable<RoutePointEvidence>? evidence = null)
        {
            if (latitude is < -90 or > 90)
                throw new ArgumentOutOfRangeException(nameof(latitude), "Latitud debe estar entre -90 y 90.");

            if (longitude is < -180 or > 180)
                throw new ArgumentOutOfRangeException(nameof(longitude), "Longitud debe estar entre -180 y 180.");

            if (!Enum.IsDefined(operation))
                throw new ArgumentOutOfRangeException(nameof(operation), "Operación de parada no válida.");

            if (!Enum.IsDefined(status))
                throw new ArgumentOutOfRangeException(nameof(status), "Estado de parada no válido.");

            return new RoutePoint(latitude, longitude, name, operation, status, evidence);
        }

        public RoutePoint WithStatus(RoutePointStatus status)
            => Create(Latitude, Longitude, Name, Operation, status, Evidence);

        public RoutePoint WithEvidence(RoutePointEvidence evidence)
            => Create(Latitude, Longitude, Name, Operation, Status, Evidence.Append(evidence));
    }
}
