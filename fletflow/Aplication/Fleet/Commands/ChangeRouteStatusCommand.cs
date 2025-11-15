using fletflow.Domain.Fleet.Entities;
using fletflow.Domain.Fleet.Repositories;
using fletflow.Infrastructure.Persistence.Contracts;

namespace fletflow.Application.Fleet.Commands
{
    public class ChangeRouteStatusCommand
    {
        private readonly IUnitOfWork _uow;
        private readonly IRouteRepository _routes;

        public ChangeRouteStatusCommand(IUnitOfWork uow, IRouteRepository routes)
        {
            _uow = uow;
            _routes = routes;
        }

        public async Task Execute(Guid id, RouteStatus status)
        {
            var route = await _routes.GetByIdAsync(id)
                ?? throw new KeyNotFoundException("Ruta no encontrada.");

            route.ChangeStatus(status);

            await _routes.UpdateAsync(route);
            await _uow.CommitAsync();
        }
    }
}
