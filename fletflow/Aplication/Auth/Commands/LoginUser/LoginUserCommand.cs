using System;
using System.Threading.Tasks;
using fletflow.Domain.Auth.Entities;
using fletflow.Infrastructure.Persistence;
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
            // 1️⃣ Buscar el usuario desde el repositorio (ya incluye roles)
            var user = await _unitOfWork.Users.GetByEmailAsync(email);

            // 2️⃣ Validar existencia
            if (user == null)
                throw new Exception("El usuario no existe.");

            // 3️⃣ Verificar contraseña (usamos BCrypt directamente)
            bool passwordValid = BCrypt.Net.BCrypt.Verify(password, user.PasswordHash);
            if (!passwordValid)
                throw new Exception("Contraseña incorrecta.");

            // 4️⃣ Retornar el usuario de dominio
            return user;
        }
    }
}
