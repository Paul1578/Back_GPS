using fletflow.Domain.Fleet.Entities;
using fletflow.Domain.Fleet.Exceptions;
using fletflow.Domain.Fleet.Repositories;
using fletflow.Infrastructure.Persistence.Contracts;

namespace fletflow.Application.Fleet.Commands;

public sealed class UnlinkDriverFromUserCommand
{
    private readonly IUnitOfWork _uow;
    private readonly IDriverRepository _drivers;
    private readonly IRouteRepository _routes;

    public UnlinkDriverFromUserCommand(IUnitOfWork uow, IDriverRepository drivers, IRouteRepository routes)
    {
        _uow = uow;
        _drivers = drivers;
        _routes = routes;
    }

    public async Task Execute(Guid driverId)
    {
        try
        {
            var initialDriver = await _drivers.GetByIdAsync(driverId)
                ?? throw new KeyNotFoundException("Conductor no encontrado.");

            await _uow.LockRouteAssignmentAsync(initialDriver.VehicleId, driverId);
            var driver = await _drivers.GetByIdAsync(driverId)
                ?? throw new KeyNotFoundException("Conductor no encontrado.");

            if (!driver.UserId.HasValue)
            {
                _uow.Rollback();
                return;
            }

            var userId = driver.UserId.Value;
            await _uow.LockLinkedUserForRouteAssignmentAsync(userId);

            var activeRoutes = await _routes.GetAllAsync(driverId: driverId, onlyActive: true);
            if (activeRoutes.Any(route => route.Status is RouteStatus.Pending or RouteStatus.InProgress))
                throw new BusinessConflictException(
                    "No se puede desvincular la cuenta de un conductor con rutas activas pendientes o en progreso.");

            driver.UnlinkUser();
            await _drivers.UpdateAsync(driver);

            // El rol Driver se conserva; revocar tokens fuerza a la cuenta a obtener
            // claims actualizados antes de volver a operar como conductor.
            await _uow.Users.IncrementTokenVersionAsync(userId);
            await _uow.RefreshTokens.RevokeAllForUserAsync(userId);

            await _uow.CommitAsync();
        }
        catch
        {
            _uow.Rollback();
            throw;
        }
    }
}
