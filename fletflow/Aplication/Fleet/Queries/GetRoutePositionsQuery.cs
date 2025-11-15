using fletflow.Application.Fleet.Dtos;
using fletflow.Application.Fleet.Mappings;
using fletflow.Domain.Fleet.Repositories;

namespace fletflow.Application.Fleet.Queries
{
    public class GetRoutePositionsQuery
    {
        private readonly IRoutePositionRepository _positions;

        public GetRoutePositionsQuery(IRoutePositionRepository positions)
        {
            _positions = positions;
        }

        public async Task<List<RoutePositionDto>> Execute(
            Guid routeId,
            DateTime? from = null,
            DateTime? to = null)
        {
            var positions = await _positions.GetByRouteAsync(routeId, from, to);
            return RoutePositionApplicationMapper.ToDtoList(positions);
        }
    }
}
