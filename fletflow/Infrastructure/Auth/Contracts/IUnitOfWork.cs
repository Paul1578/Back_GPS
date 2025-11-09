using System.Threading.Tasks;
using fletflow.Domain.Auth.Repositories;

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
        void Rollback();
    }
}
