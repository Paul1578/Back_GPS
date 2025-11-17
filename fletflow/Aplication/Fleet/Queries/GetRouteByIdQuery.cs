using fletflow.Application.Fleet.Dtos;
using fletflow.Application.Fleet.Mappings;
using fletflow.Domain.Fleet.Repositories;

namespace fletflow.Application.Fleet.Queries
{
    public class GetRouteByIdQuery
    {
        private readonly IRouteRepository _routes;

        public GetRouteByIdQuery(IRouteRepository routes)
        {
            _routes = routes;
        }

        public async Task<RouteDto> Execute(Guid id)
        {
            var route = await _routes.GetByIdAsync(id)
                ?? throw new KeyNotFoundException("Ruta no encontrada.");

            return RouteApplicationMapper.ToDto(route);
        }
    }
}
