using fletflow.Infrastructure.Persistence.Contracts;
using fletflow.Domain.Fleet.Entities;
using fletflow.Domain.Fleet.Exceptions;

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

        try
        {
            if (await _uow.Users.GetByIdAsync(userId) is null)
                throw new KeyNotFoundException("Usuario no encontrado.");
            var initialDriver = await _uow.Drivers.GetByUserIdAsync(userId);
            await _uow.LockRouteAssignmentAsync(initialDriver?.VehicleId, initialDriver?.Id);
            // Share the user-row lock with refresh rotation before revoking sessions.
            await _uow.LockLinkedUserForRouteAssignmentAsync(userId);

            // Re-read after taking the same assignment locks used by route start.
            var user = await _uow.Users.GetByIdAsync(userId)
                ?? throw new KeyNotFoundException("Usuario no encontrado.");
            var linkedDriver = await _uow.Drivers.GetByUserIdAsync(userId);
            var role = await _uow.Roles.GetByNameAsync(roleName)
                ?? throw new KeyNotFoundException($"Rol '{roleName}' no existe.");
            var roles = await _uow.Roles.GetAllAsync();
            var currentRoleNames = user.UserRoles.Select(x => x.Role.Name).ToArray();
            var roleChanged = currentRoleNames.Length != 1 ||
                !currentRoleNames[0].Equals(role.Name, StringComparison.OrdinalIgnoreCase);

            if (!roleName.Equals("Driver", StringComparison.OrdinalIgnoreCase) && linkedDriver?.IsActive == true)
            {
                var activeRoutes = await _uow.Routes.GetAllAsync(driverId: linkedDriver.Id, onlyActive: true);
                if (activeRoutes.Any(route => route.Status is RouteStatus.Pending or RouteStatus.InProgress))
                    throw new BusinessConflictException("No se puede cambiar el rol mientras el conductor tenga rutas activas pendientes o en progreso.");
                linkedDriver.Deactivate();
                linkedDriver.UnassignVehicle();
                await _uow.Drivers.UpdateAsync(linkedDriver);
            }
            else if (user.IsActive && roleName.Equals("Driver", StringComparison.OrdinalIgnoreCase) && linkedDriver is { IsActive: false })
            {
                linkedDriver.Activate();
                await _uow.Drivers.UpdateAsync(linkedDriver);
            }

            foreach (var currentRole in roles)
            {
                if (!string.Equals(currentRole.Name, role.Name, StringComparison.OrdinalIgnoreCase))
                    await _uow.Users.RemoveRoleAsync(userId, currentRole.Id);
            }

            await _uow.Users.AssignRoleAsync(userId, role.Id);
            if (roleChanged)
            {
                // Invalida access JWT incluso si posteriormente se restaura el rol original.
                await _uow.Users.IncrementTokenVersionAsync(userId);
                await _uow.RefreshTokens.RevokeAllForUserAsync(userId);
            }
            await _uow.CommitAsync();
        }
        catch
        {
            _uow.Rollback();
            throw;
        }
    }
}
