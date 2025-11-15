using fletflow.Application.Fleet.Dtos;
using fletflow.Application.Fleet.Mappings;
using fletflow.Domain.Fleet.Entities;
using fletflow.Domain.Fleet.Repositories;
using fletflow.Infrastructure.Persistence.Contracts;

namespace fletflow.Application.Fleet.Commands
{
    public class RegisterRoutePositionCommand
    {
        private readonly IUnitOfWork _uow;
        private readonly IRouteRepository _routes;
        private readonly IRoutePositionRepository _positions;

        public RegisterRoutePositionCommand(
            IUnitOfWork uow,
            IRouteRepository routes,
            IRoutePositionRepository positions)
        {
            _uow = uow;
            _routes = routes;
            _positions = positions;
        }

        public async Task<RoutePositionDto> Execute(
            Guid routeId,
            double latitude,
            double longitude,
            DateTime? recordedAt,
            double? speedKmh,
            double? heading)
        {
            var route = await _routes.GetByIdAsync(routeId)
                ?? throw new KeyNotFoundException("Ruta no encontrada.");

            if (!route.IsActive)
                throw new InvalidOperationException("No se puede registrar posiciones en una ruta inactiva.");

            var position = RoutePosition.Create(
                routeId,
                latitude,
                longitude,
                recordedAt,
                speedKmh,
                heading
            );

            await _positions.AddAsync(position);
            await _uow.CommitAsync();

            return RoutePositionApplicationMapper.ToDto(position);
        }
    }
}
