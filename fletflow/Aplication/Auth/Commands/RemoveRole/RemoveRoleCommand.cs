using fletflow.Domain.Auth.Repositories;
using fletflow.Infrastructure.Persistence.Contracts;

namespace fletflow.Application.Auth.Commands
{
    public class RemoveRoleFromUserCommand
    {
        private readonly IUnitOfWork _uow;
        private readonly IUserRepository _users;
        private readonly IRoleRepository _roles;

        public RemoveRoleFromUserCommand(IUnitOfWork uow, IUserRepository users, IRoleRepository roles)
        {
            _uow = uow;
            _users = users;
            _roles = roles;
        }

        public async Task Execute(Guid userId, string roleName)
        {
            await Task.CompletedTask;
            throw new InvalidOperationException("No se puede quitar un rol por separado; reemplaza el rol en una sola operación.");
        }
    }
}
