namespace fletflow.Domain.Fleet.Entities
{
    public class RoutePosition
    {
        public Guid Id { get; private set; }
        public Guid RouteId { get; private set; }

        public double Latitude { get; private set; }
        public double Longitude { get; private set; }

        public DateTime RecordedAt { get; private set; } // ideal en UTC
        public double? SpeedKmh { get; private set; }    // opcional
        public double? Heading { get; private set; }     // opcional (0-360)

        private RoutePosition() { }

        private RoutePosition(
            Guid id,
            Guid routeId,
            double latitude,
            double longitude,
            DateTime recordedAt,
            double? speedKmh,
            double? heading)
        {
            Id = id;
            RouteId = routeId;
            Latitude = latitude;
            Longitude = longitude;
            RecordedAt = recordedAt;
            SpeedKmh = speedKmh;
            Heading = heading;
        }

        public static RoutePosition Create(
            Guid routeId,
            double latitude,
            double longitude,
            DateTime? recordedAt = null,
            double? speedKmh = null,
            double? heading = null)
        {
            // reglas básicas de dominio
            if (latitude is < -90 or > 90)
                throw new ArgumentOutOfRangeException(nameof(latitude), "Latitud debe estar entre -90 y 90.");

            if (longitude is < -180 or > 180)
                throw new ArgumentOutOfRangeException(nameof(longitude), "Longitud debe estar entre -180 y 180.");

            // si no envían fecha, la ponemos ahora en UTC
            var ts = recordedAt?.ToUniversalTime() ?? DateTime.UtcNow;

            return new RoutePosition(
                Guid.NewGuid(),
                routeId,
                latitude,
                longitude,
                ts,
                speedKmh,
                heading
            );
        }

        public static RoutePosition CreateExisting(
            Guid id,
            Guid routeId,
            double latitude,
            double longitude,
            DateTime recordedAt,
            double? speedKmh,
            double? heading)
        {
            return new RoutePosition(
                id,
                routeId,
                latitude,
                longitude,
                recordedAt,
                speedKmh,
                heading
            );
        }
    }
}
