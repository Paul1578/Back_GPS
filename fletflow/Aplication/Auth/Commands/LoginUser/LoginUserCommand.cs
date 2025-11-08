using fletflow.Domain.Auth.Entities;
using fletflow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace fletflow.Aplication.Auth.Commands
{
    public class LoginUserCommand
    {
        private readonly AppDbContext _context;

        public LoginUserCommand(AppDbContext context)
        {
            _context = context;
        }

        public async Task<User> Execute(string email, string password)
        {
            // 1️⃣ Buscar usuario por email e incluir sus roles
            var user = await _context.Users
                .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                .FirstOrDefaultAsync(u => u.Email == email);

            // 2️⃣ Validar existencia
            if (user == null)
                throw new Exception("El usuario no existe.");

            // 3️⃣ Verificar contraseña
            bool passwordValid = BCrypt.Net.BCrypt.Verify(password, user.PasswordHash);
            if (!passwordValid)
                throw new Exception("Contraseña incorrecta.");

            // 4️⃣ Retornar usuario (con roles cargados)
            return user;
        }
    }
}
