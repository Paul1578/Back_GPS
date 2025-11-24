using System;
using System.Threading.Tasks;
using fletflow.Domain.Auth.Entities;
using fletflow.Infrastructure.Persistence.Contracts;

namespace fletflow.Application.Auth.Commands
{
    public class LoginUserCommand
    {
        private readonly IUnitOfWork _unitOfWork;

        public LoginUserCommand(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<User> Execute(string email, string password)
        {
            var user = await _unitOfWork.Users.GetByEmailAsync(email);
            if (user == null)
                throw new Exception("El usuario no existe.");

            var passwordValid = BCrypt.Net.BCrypt.Verify(password, user.PasswordHash);
            if (!passwordValid)
                throw new Exception("Contrase\u00f1a incorrecta.");

            if (user.MustChangePassword)
                throw new Exception("Debe activar su cuenta antes de iniciar sesion.");

            return user;
        }
    }
}
