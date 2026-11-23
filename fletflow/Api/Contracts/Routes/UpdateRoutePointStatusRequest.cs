using fletflow.Domain.Fleet.Entities;

namespace fletflow.Api.Contracts.Routes;

public sealed class UpdateRoutePointStatusRequest
{
    public RoutePointStatus Status { get; set; }
}
