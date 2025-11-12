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
            var user = await _users.GetByIdAsync(userId) ?? throw new KeyNotFoundException("Usuario no encontrado.");
            var role = await _roles.GetByNameAsync(roleName.Trim()) ?? throw new KeyNotFoundException($"Rol '{roleName}' no existe.");

            await _users.RemoveRoleAsync(user.Id, role.Id);
            await _uow.CommitAsync();
        }
    }
}
