using fletflow.Domain.Auth.Entities;
using fletflow.Infrastructure.Persistence.Entities;

namespace fletflow.Infrastructure.Persistence.Mappings
{
    public static class UserMapper
    {
        public static User ToDomain(this UserEntity entity)
        {
            return new User
            {
                Id = entity.Id,
                Username = entity.Username,
                Email = entity.Email,
                PasswordHash = entity.PasswordHash,
                IsActive = entity.IsActive,
                UserRoles = entity.UserRoles
                    .Select(ur => new UserRole
                    {
                        Role = new Role { Id = ur.Role.Id, Name = ur.Role.Name }
                    }).ToList()
            };
        }

        public static UserEntity ToEntity(this User domain)
        {
            return new UserEntity
            {
                Id = domain.Id,
                Username = domain.Username,
                Email = domain.Email,
                PasswordHash = domain.PasswordHash,
                IsActive = domain.IsActive,
                UserRoles = domain.UserRoles?.Select(ur => new UserRoleEntity
                {
                    UserId = ur.UserId,
                    RoleId = ur.RoleId,
                    // ❌ NO asignar ur.Role aquí; evita que EF intente insertar el rol de nuevo
                }).ToList() ?? new List<UserRoleEntity>()
            };
        }

    }
}
