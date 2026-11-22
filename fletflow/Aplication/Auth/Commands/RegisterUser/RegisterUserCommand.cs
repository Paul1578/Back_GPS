using fletflow.Domain.Auth.Entities;
using fletflow.Infrastructure.Persistence;
using fletflow.Infrastructure.Persistence.Contracts;
using fletflow.Domain.Fleet.Exceptions;

namespace fletflow.Application.Auth.Commands
{
    public class RegisterUserCommand
    {
        private readonly IUnitOfWork _unitOfWork;

        public RegisterUserCommand(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<User> Execute(
            string username,
            string email,
            string password,
            string roleName,
            Guid? ownerUserId = null,
            bool mustChangePassword = false,
            bool saveChanges = true,
            string? firstName = null,
            string? lastName = null)
        {
            username = username.Trim();
            email = email.Trim();
            firstName = string.IsNullOrWhiteSpace(firstName) ? null : firstName.Trim();
            lastName = string.IsNullOrWhiteSpace(lastName) ? null : lastName.Trim();
            if (username.Length > 100)
                throw new ArgumentException("El usuario no puede exceder 100 caracteres.");
            if (email.Length > 200)
                throw new ArgumentException("El correo no puede exceder 200 caracteres.");
            if (firstName?.Length > 100 || lastName?.Length > 100)
                throw new ArgumentException("Nombres y apellidos no pueden exceder 100 caracteres.");

            // Hacer el mismo trabajo de password hashing antes del pre-check reduce
            // la diferencia temporal entre un email nuevo y uno ya registrado.
            var passwordHash = BCrypt.Net.BCrypt.HashPassword(password);

            // 1) Verificar si el usuario ya existe
            var existingUser = await _unitOfWork.Users.GetByEmailAsync(email);
            if (existingUser is not null)
                throw new BusinessConflictException("El correo ya fue registrado.");

            // 2) Normalizar rol
            roleName = string.IsNullOrWhiteSpace(roleName) ? "User" : roleName.Trim();

            // 3) Resolver (o crear) rol por nombre
            var role = await _unitOfWork.Roles.GetByNameAsync(roleName);
            if (role is null)
            {
                role = new Role { Id = Guid.NewGuid(), Name = roleName };
                await _unitOfWork.Roles.AddAsync(role);
            }

            // 4) Crear usuario de dominio
            var user = new User
            {
                Id = Guid.NewGuid(),
                Username = username,
                FirstName = string.IsNullOrWhiteSpace(firstName) ? null : firstName.Trim(),
                LastName = string.IsNullOrWhiteSpace(lastName) ? null : lastName.Trim(),
                Email = email,
                PasswordHash = passwordHash,
                IsActive = true,
                MustChangePassword = mustChangePassword,
                OwnerUserId = ownerUserId 
            };

            // 5) Relación por RoleId (clave: NO insertar de nuevo el role)
            user.UserRoles.Add(new UserRole
            {
                UserId = user.Id,
                RoleId = role.Id,
                Role = role, // opcional en dominio; NO se mapea a entity al guardar
                User = user
            });

            // 6) Guardar usuario
            await _unitOfWork.Users.AddAsync(user);
            if (saveChanges)
                await _unitOfWork.CommitAsync();

            return user;
        }
    }
}
