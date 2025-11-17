using fletflow.Application.Fleet.Dtos;
using fletflow.Application.Fleet.Mappings;
using fletflow.Domain.Fleet.Entities;
using fletflow.Domain.Fleet.Repositories;

namespace fletflow.Application.Fleet.Queries
{
    public class GetRoutesQuery
    {
        private readonly IRouteRepository _routes;

        public GetRoutesQuery(IRouteRepository routes)
        {
            _routes = routes;
        }

        public async Task<List<RouteDto>> Execute(
            Guid? vehicleId = null,
            Guid? driverId = null,
            RouteStatus? status = null)
        {
            var routes = await _routes.GetAllAsync(vehicleId, driverId, status);
            return RouteApplicationMapper.ToDtoList(routes);
        }
    }
}
