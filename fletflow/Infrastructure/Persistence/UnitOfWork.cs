using fletflow.Domain.Auth.Repositories;
using fletflow.Domain.Fleet.Repositories;
using fletflow.Infrastructure.Persistence.Context;
using fletflow.Infrastructure.Persistence.Contracts;

namespace fletflow.Infrastructure.Persistence
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly AppDbContext _context;

        public IUserRepository Users { get; }
        public IRoleRepository Roles { get; }
        public IRefreshTokenRepository RefreshTokens { get; }
        public IVehicleRepository Vehicles { get; }
        public IDriverRepository Drivers { get; }
        public IRouteRepository Routes { get; }

        public UnitOfWork(
            AppDbContext context,
            IUserRepository users,
            IRoleRepository roles,
            IRefreshTokenRepository rts,
            IPasswordResetTokenRepository passwordResetTokens,
            IVehicleRepository vehicleRepository,
            IDriverRepository drivers,
            IRouteRepository routes)
        {
            _context = context;
            Users = users;
            Roles = roles;
            RefreshTokens = rts;
            PasswordResetTokens = passwordResetTokens;
            Vehicles = vehicleRepository;
            Drivers = drivers;
            Routes = routes;
        }

        public Task<int> CommitAsync() => _context.SaveChangesAsync();

        public IPasswordResetTokenRepository PasswordResetTokens { get; }

        public void Rollback() { /* opcional: manejar transacciones explícitas si las usas */ }
    }
}
