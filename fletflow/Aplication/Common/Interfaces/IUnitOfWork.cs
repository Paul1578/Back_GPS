
using fletflow.Domain.Auth.Repositories;
using fletflow.Domain.Fleet.Repositories;

namespace fletflow.Infrastructure.Persistence.Contracts
{
    public interface IUnitOfWork
    {
        IUserRepository Users { get; }

        /// <summary>
        /// Persiste todos los cambios pendientes de esta unidad de trabajo.
        /// </summary>
        IRoleRepository Roles { get; }

        Task<int> CommitAsync();

        /// <summary>
        /// Descarta cambios en el DbContext (si procede).
        /// </summary>
        
        /// 
        IRefreshTokenRepository RefreshTokens { get; }

        IPasswordResetTokenRepository PasswordResetTokens { get; }

        IVehicleRepository Vehicles { get; } 

        IDriverRepository Drivers { get; }
        IRouteRepository Routes { get; }
        IRoutePositionRepository RoutePositions { get; }
        void Rollback();
    }
}
