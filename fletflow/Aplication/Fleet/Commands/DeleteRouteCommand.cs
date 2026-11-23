using fletflow.Domain.Fleet.Repositories;
using fletflow.Domain.Fleet.Entities;
using fletflow.Domain.Fleet.Exceptions;
using fletflow.Infrastructure.Persistence.Contracts;

namespace fletflow.Application.Fleet.Commands
{
    public class DeleteRouteCommand
    {
        private readonly IUnitOfWork _uow;
        private readonly IRouteRepository _routes;

        public DeleteRouteCommand(IUnitOfWork uow, IRouteRepository routes)
        {
            _uow = uow;
            _routes = routes;
        }

        public async Task Execute(Guid id)
        {
            try
            {
                var route = await _routes.GetByIdAsync(id)
                    ?? throw new KeyNotFoundException("Route not found.");
                await _uow.LockRouteAssignmentAsync(route.VehicleId, route.DriverId, id);
                route = await _routes.GetByIdAsync(id)
                    ?? throw new KeyNotFoundException("Route not found.");

                if (route.Status == RouteStatus.InProgress)
                    throw new BusinessConflictException("No se puede dar de baja una ruta en progreso. Cancélala primero.");

                route.Deactivate();
                await _routes.UpdateAsync(route);
                await _uow.CommitAsync();
            }
            catch
            {
                _uow.Rollback();
                throw;
            }
        }
    }
}
