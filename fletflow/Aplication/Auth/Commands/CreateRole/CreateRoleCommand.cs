using fletflow.Domain.Auth.Entities;
using fletflow.Domain.Auth.Repositories;
using fletflow.Infrastructure.Persistence.Contracts;

namespace fletflow.Application.Auth.Commands
{
    public class CreateRoleCommand
    {
        private readonly IUnitOfWork _uow;
        private readonly IRoleRepository _roles;

        public CreateRoleCommand(IUnitOfWork uow, IRoleRepository roles)
        {
            _uow = uow;
            _roles = roles;
        }

        public async Task Execute(string name)
        {
            var roleName = name.Trim();
            var exists = await _roles.GetByNameAsync(roleName);
            if (exists is not null)
                throw new InvalidOperationException($"El rol '{roleName}' ya existe.");

            await _roles.AddAsync(new Role { Id = Guid.NewGuid(), Name = roleName });
            await _uow.CommitAsync();
        }
    }
}
