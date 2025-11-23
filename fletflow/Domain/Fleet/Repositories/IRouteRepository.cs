using fletflow.Domain.Fleet.Entities;

namespace fletflow.Domain.Fleet.Repositories
{
    public interface IRouteRepository
    {
        Task<RouteE?> GetByIdAsync(Guid id);
        Task AddAsync(RouteE route);
        Task UpdateAsync(RouteE route);
        Task<List<RouteE>> GetAllAsync(
            Guid? vehicleId = null,
            Guid? driverId = null,
            RouteStatus? status = null,
            bool? onlyActive = null);
    }
}
