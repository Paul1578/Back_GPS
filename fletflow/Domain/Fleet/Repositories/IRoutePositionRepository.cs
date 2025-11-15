using fletflow.Domain.Fleet.Entities;

namespace fletflow.Domain.Fleet.Repositories
{
    public interface IRoutePositionRepository
    {
        Task AddAsync(RoutePosition position);

        Task<List<RoutePosition>> GetByRouteAsync(
            Guid routeId,
            DateTime? from = null,
            DateTime? to = null);
    }
}
