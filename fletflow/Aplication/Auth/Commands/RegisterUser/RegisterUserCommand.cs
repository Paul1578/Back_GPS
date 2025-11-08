using fletflow.Domain.Auth.Entities;
using fletflow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace fletflow.Aplication.Auth.Commands
{
    public class RegisterUserCommand
    {
        private readonly AppDbContext _context;

        public RegisterUserCommand(AppDbContext context)
        {
            _context = context;
        }

        public async Task<User> Execute(string username, string email, string password, string roleName)
        {
            // 1️⃣ Verificar si el usuario ya existe
            if (await _context.Users.AnyAsync(u => u.Email == email))
                throw new Exception("El correo ya fue registrado.");

            // 2️⃣ Crear el usuario
            var user = new User
            {
                Username = username,
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password)
            };

            // 🔥 Normalizar el nombre del rol (elimina espacios/saltos y lo pone con mayúscula inicial)
            roleName = roleName.Trim();

            // 3️⃣ Buscar el rol sin importar mayúsculas/minúsculas
            var role = await _context.Roles
                .FirstOrDefaultAsync(r => r.Name.ToLower() == roleName.ToLower());

            // 4️⃣ Si no existe, créalo
            if (role == null)
            {
                role = new Role { Name = roleName };
                _context.Roles.Add(role);
            }

            // 5️⃣ Asignar el rol al usuario
            user.UserRoles.Add(new UserRole
            {
                User = user,
                Role = role
            });

            // 6️⃣ Guardar en DB
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return user;
        }

    }
}
