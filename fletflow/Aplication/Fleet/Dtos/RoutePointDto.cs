using fletflow.Domain.Fleet.Entities;

namespace fletflow.Application.Fleet.Dtos
{
    public class RoutePointDto
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string? Name { get; set; }
        public RoutePointOperation Operation { get; set; }
        public RoutePointStatus Status { get; set; }
        public List<RoutePointEvidenceDto> Evidence { get; set; } = new();
    }

    public class RoutePointEvidenceDto
    {
        public string Type { get; set; } = string.Empty;
        public string? Content { get; set; }
        public string? Description { get; set; }
        public DateTime CreatedAtUtc { get; set; }
    }
}
