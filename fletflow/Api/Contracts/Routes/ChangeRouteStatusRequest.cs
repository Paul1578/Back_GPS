using fletflow.Domain.Fleet.Entities;

namespace fletflow.Api.Contracts.Routes
{
    public class ChangeRouteStatusRequest
    {
        public RouteStatus Status { get; set; }
    }
}
