using fletflow.Domain.Auth.Repositories;
using fletflow.Infrastructure.Persistence.Context;
using fletflow.Infrastructure.Persistence.Contracts;

namespace fletflow.Infrastructure.Persistence
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly AppDbContext _context;

        public IUserRepository Users { get; }
        public IRoleRepository Roles { get; }

        public UnitOfWork(AppDbContext context, IUserRepository users, IRoleRepository roles)
        {
            _context = context;
            Users = users;
            Roles = roles;
        }

        public Task<int> CommitAsync() => _context.SaveChangesAsync();

        public void Rollback() { /* opcional: manejar transacciones explícitas si las usas */ }
    }
}
