using fletflow.Infrastructure.Persistence.Contracts;

namespace fletflow.Application.Auth.Commands;

public class ChangeUserRoleCommand
{
    private static readonly HashSet<string> AssignableRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        "Admin", "Manager", "Logistics", "Driver", "User"
    };

    private readonly IUnitOfWork _uow;

    public ChangeUserRoleCommand(IUnitOfWork uow) => _uow = uow;

    public async Task Execute(Guid userId, string requestedRole)
    {
        var roleName = requestedRole.Trim();
        if (!AssignableRoles.Contains(roleName))
            throw new ArgumentException("El rol solicitado no está permitido.");

        if (await _uow.Users.GetByIdAsync(userId) is null)
            throw new KeyNotFoundException("Usuario no encontrado.");
        var role = await _uow.Roles.GetByNameAsync(roleName)
            ?? throw new KeyNotFoundException($"Rol '{roleName}' no existe.");
        var currentRoles = await _uow.Roles.GetAllAsync();
        var linkedDriver = await _uow.Drivers.GetByUserIdAsync(userId);

        if (!roleName.Equals("Driver", StringComparison.OrdinalIgnoreCase) && linkedDriver?.IsActive == true)
        {
            var activeRoutes = await _uow.Routes.GetAllAsync(
                driverId: linkedDriver.Id,
                status: fletflow.Domain.Fleet.Entities.RouteStatus.InProgress,
                onlyActive: true);
            if (activeRoutes.Count > 0)
                throw new InvalidOperationException("No se puede cambiar el rol mientras el conductor tenga una ruta en curso.");
            linkedDriver.Deactivate();
            linkedDriver.UnassignVehicle();
            await _uow.Drivers.UpdateAsync(linkedDriver);
        }
        else if (roleName.Equals("Driver", StringComparison.OrdinalIgnoreCase) && linkedDriver is { IsActive: false })
        {
            linkedDriver.Activate();
            await _uow.Drivers.UpdateAsync(linkedDriver);
        }

        foreach (var currentRole in currentRoles)
        {
            if (!string.Equals(currentRole.Name, role.Name, StringComparison.OrdinalIgnoreCase))
                await _uow.Users.RemoveRoleAsync(userId, currentRole.Id);
        }

        await _uow.Users.AssignRoleAsync(userId, role.Id);
        await _uow.CommitAsync();
    }
}
