using fletflow.Domain.Auth.Entities;
using fletflow.Infrastructure.Persistence;
using fletflow.Infrastructure.Persistence.Contracts;

namespace fletflow.Application.Auth.Commands
{
    public class RegisterUserCommand
    {
        private readonly IUnitOfWork _unitOfWork;

        public RegisterUserCommand(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<User> Execute(string username, string email, string password, string roleName, Guid? ownerUserId = null)
        {
            // 1) Verificar si el usuario ya existe
            var existingUser = await _unitOfWork.Users.GetByEmailAsync(email);
            if (existingUser is not null)
                throw new Exception("El correo ya fue registrado.");

            // 2) Normalizar rol
            roleName = string.IsNullOrWhiteSpace(roleName) ? "User" : roleName.Trim();

            // 3) Resolver (o crear) rol por nombre
            var role = await _unitOfWork.Roles.GetByNameAsync(roleName);
            if (role is null)
            {
                role = new Role { Id = Guid.NewGuid(), Name = roleName };
                await _unitOfWork.Roles.AddAsync(role);
                await _unitOfWork.CommitAsync(); // asegura Role.Id persistido
            }

            // 4) Crear usuario de dominio
            var user = new User
            {
                Id = Guid.NewGuid(),
                Username = username,
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                IsActive = true,
                OwnerUserId = ownerUserId 
            };

            // 5) Relación por RoleId (clave: NO insertar de nuevo el role)
            user.UserRoles.Add(new UserRole
            {
                UserId = user.Id,
                RoleId = role.Id,
                Role = role // opcional en dominio; NO se mapea a entity al guardar
            });

            // 6) Guardar usuario
            await _unitOfWork.Users.AddAsync(user);
            await _unitOfWork.CommitAsync();

            return user;
        }
    }
}
