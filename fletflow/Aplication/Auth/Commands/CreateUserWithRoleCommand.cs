using fletflow.Domain.Auth.Entities;

namespace fletflow.Application.Auth.Commands
{
    /// <summary>
    /// Caso de uso: un administrador crea un usuario y le asigna un rol.
    /// Reutiliza RegisterUserCommand, que ya maneja roles y password hash.
    /// </summary>
    public class CreateUserWithRoleCommand
    {
        private readonly RegisterUserCommand _registerUserCommand;

        public CreateUserWithRoleCommand(RegisterUserCommand registerUserCommand)
        {
            _registerUserCommand = registerUserCommand;
        }

        public async Task<User> Execute(string username, string email, string password, string roleName)
        {
            // Delegamos TODA la lógica en tu RegisterUserCommand
            var user = await _registerUserCommand.Execute(username, email, password, roleName);
            return user;
        }
    }
}
