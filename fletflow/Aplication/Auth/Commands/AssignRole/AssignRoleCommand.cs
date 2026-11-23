namespace fletflow.Application.Auth.Commands
{
    public class AssignRoleToUserCommand
    {
        private readonly ChangeUserRoleCommand _changeRole;

        public AssignRoleToUserCommand(ChangeUserRoleCommand changeRole) => _changeRole = changeRole;

        public Task Execute(Guid userId, string roleName) => _changeRole.Execute(userId, roleName);
    }
}
