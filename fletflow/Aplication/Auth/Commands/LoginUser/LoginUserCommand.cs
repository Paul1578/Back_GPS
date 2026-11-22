using System;
using System.Threading.Tasks;
using fletflow.Domain.Auth.Entities;
using fletflow.Infrastructure.Persistence.Contracts;

namespace fletflow.Application.Auth.Commands
{
    public class LoginUserCommand
    {
        private const string InvalidCredentialsMessage = "Correo o contraseña incorrectos.";
        private static readonly string DummyPasswordHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString("N"));
        private readonly IUnitOfWork _unitOfWork;

        public LoginUserCommand(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<User> Execute(string email, string password)
        {
            var user = await _unitOfWork.Users.GetByEmailAsync(email);
            // También ejecuta BCrypt si el usuario no existe para reducir la diferencia
            // temporal entre un correo registrado y uno inexistente.
            var passwordValid = BCrypt.Net.BCrypt.Verify(password, user?.PasswordHash ?? DummyPasswordHash);
            if (user is null || !passwordValid || !user.IsActive || user.MustChangePassword)
                throw new UnauthorizedAccessException(InvalidCredentialsMessage);

            return user;
        }
    }
}
