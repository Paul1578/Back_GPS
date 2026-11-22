using fletflow.Domain.Fleet.Entities;
using fletflow.Domain.Fleet.Repositories;
using fletflow.Infrastructure.Persistence.Contracts;

namespace fletflow.Application.Fleet.Commands;

public class ChangeDriverStatusCommand
{
    private readonly IUnitOfWork _uow;
    private readonly IDriverRepository _drivers;
    private readonly IRouteRepository _routes;

    public ChangeDriverStatusCommand(IUnitOfWork uow, IDriverRepository drivers, IRouteRepository routes)
    {
        _uow = uow;
        _drivers = drivers;
        _routes = routes;
    }

    public async Task Execute(Guid id, bool isActive)
    {
        var driver = await _drivers.GetByIdAsync(id)
            ?? throw new KeyNotFoundException("Conductor no encontrado.");
        if (driver.IsActive == isActive) return;

        if (!isActive)
        {
            var activeRoutes = await _routes.GetAllAsync(driverId: id, status: RouteStatus.InProgress, onlyActive: true);
            if (activeRoutes.Count > 0)
                throw new InvalidOperationException("No se puede desactivar un conductor con una ruta en curso.");
            driver.Deactivate();
            driver.UnassignVehicle();
        }
        else
        {
            driver.Activate();
        }

        await _drivers.UpdateAsync(driver);
        await _uow.CommitAsync();
    }
}
