using fletflow.Domain.Auth.Repositories;
using fletflow.Infrastructure.Persistence.Contracts;

namespace fletflow.Application.Auth.Commands
{
    public class DeleteRoleCommand
    {
        private readonly IUnitOfWork _uow;
        private readonly IRoleRepository _roles;

        public DeleteRoleCommand(IUnitOfWork uow, IRoleRepository roles)
        {
            _uow = uow;
            _roles = roles;
        }

        public async Task Execute(string name)
        {
            var ok = await _roles.DeleteByNameAsync(name);
            if (!ok) throw new KeyNotFoundException($"El rol '{name}' no existe.");

            await _uow.CommitAsync();
        }
    }
}
