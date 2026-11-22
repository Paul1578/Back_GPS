using fletflow.Domain.Fleet.Entities;

namespace fletflow.Api.Contracts.Routes
{
    public class RoutePointRequest
    {
        public double Lat { get; set; }
        public double Lng { get; set; }
        public string? Name { get; set; }
        public RoutePointOperation Operation { get; set; } = RoutePointOperation.None;
        public RoutePointStatus Status { get; set; } = RoutePointStatus.Pending;
    }
}
