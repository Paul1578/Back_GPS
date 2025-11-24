namespace fletflow.Domain.Fleet.Entities
{
    public class RoutePoint
    {
        public double Latitude { get; }
        public double Longitude { get; }
        public string? Name { get; }

        private RoutePoint(double latitude, double longitude, string? name)
        {
            Latitude = latitude;
            Longitude = longitude;
            Name = string.IsNullOrWhiteSpace(name) ? null : name.Trim();
        }

        public static RoutePoint Create(double latitude, double longitude, string? name = null)
        {
            if (latitude is < -90 or > 90)
                throw new ArgumentOutOfRangeException(nameof(latitude), "Latitud debe estar entre -90 y 90.");

            if (longitude is < -180 or > 180)
                throw new ArgumentOutOfRangeException(nameof(longitude), "Longitud debe estar entre -180 y 180.");

            return new RoutePoint(latitude, longitude, name);
        }
    }
}
