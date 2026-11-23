using fletflow.Infrastructure.Persistence.Contracts;
using fletflow.Infrastructure.Persistence.Context;
using fletflow.Domain.Fleet.Entities;
using fletflow.Domain.Fleet.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace fletflow.Application.Users.Commands
{
    public class UpdateUserCommand
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly AppDbContext _context;

        public UpdateUserCommand(IUnitOfWork unitOfWork, AppDbContext context)
        {
            _unitOfWork = unitOfWork;
            _context = context;
        }

        public async Task Execute(Guid userId, bool isActive)
        {
            try
            {
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId)
                    ?? throw new KeyNotFoundException("Usuario no encontrado.");
                var initialDriver = await _unitOfWork.Drivers.GetByUserIdAsync(userId);

                if (initialDriver is not null)
                {
                    await _unitOfWork.LockRouteAssignmentAsync(initialDriver.VehicleId, initialDriver.Id);
                    await _unitOfWork.LockLinkedUserForRouteAssignmentAsync(userId);

                    // Re-read after the assignment locks; a route start takes the same locks.
                    var linkedDriver = await _unitOfWork.Drivers.GetByUserIdAsync(userId);
                    if (linkedDriver?.Id != initialDriver.Id)
                        throw new BusinessConflictException("La asignación del conductor cambió; vuelve a intentar.");

                    await _context.Entry(user).ReloadAsync();
                    if (!isActive)
                    {
                        var activeRoutes = await _unitOfWork.Routes.GetAllAsync(
                            driverId: linkedDriver.Id,
                            onlyActive: true);
                        if (activeRoutes.Any(route => route.Status is RouteStatus.Pending or RouteStatus.InProgress))
                            throw new BusinessConflictException(
                                "No se puede desactivar la cuenta mientras el conductor tenga rutas activas pendientes o en progreso.");
                    }
                }
                else
                {
                    // Serialize user state changes with refresh-token rotation, which locks
                    // this same row before inserting a replacement token.
                    await _unitOfWork.LockUserSessionAsync(userId);
                    await _context.Entry(user).ReloadAsync();
                }

                if (user.IsActive == isActive)
                {
                    _unitOfWork.Rollback();
                    return;
                }

                user.IsActive = isActive;
                // Access JWT are stateless; bump the persisted version on both deactivation
                // and reactivation so a token rejected while inactive cannot become valid again.
                user.TokenVersion++;
                if (!isActive)
                    await _unitOfWork.RefreshTokens.RevokeAllForUserAsync(userId);
                await _unitOfWork.CommitAsync();
            }
            catch
            {
                _unitOfWork.Rollback();
                throw;
            }
        }
    }
}
