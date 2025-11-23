using fletflow.Domain.Fleet.Repositories;
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
            var route = await _routes.GetByIdAsync(id)
                ?? throw new KeyNotFoundException("Ruta no encontrada.");

            // Baja lógica
            route.Deactivate();

            await _routes.UpdateAsync(route);
            await _uow.CommitAsync();
        }
    }
}
