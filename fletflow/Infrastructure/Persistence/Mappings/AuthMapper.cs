using System.Collections.Generic;
using System.Linq;
using fletflow.Domain.Auth.Entities;
using fletflow.Infrastructure.Persistence.Entities;

namespace fletflow.Infrastructure.Persistence.Mappings
{
    public static class AuthMapper
    {
        // ===========================
        // Infra -> Dominio
        // ===========================
        public static User ToDomain(this UserEntity entity)
        {
            if (entity is null) return null!;

            return new User
            {
                Id = entity.Id,
                Username = entity.Username,
                Email = entity.Email,
                PasswordHash = entity.PasswordHash,
                IsActive = entity.IsActive,
                MustChangePassword = entity.MustChangePassword,
                OwnerUserId = entity.OwnerUserId,
                UserRoles = entity.UserRoles?.Select(ur => new UserRole
                {
                    UserId = ur.UserId,
                    RoleId = ur.RoleId,
                    Role = ur.Role is null ? null! : new Role
                    {
                        Id = ur.Role.Id,
                        Name = ur.Role.Name
                    }
                }).ToList() ?? new List<UserRole>()
            };
        }

        public static Role ToDomain(this RoleEntity entity)
        {
            if (entity is null) return null!;
            return new Role
            {
                Id = entity.Id,
                Name = entity.Name
            };
        }

        // (Opcional) Si necesitas mapear UserRole individualmente
        public static UserRole ToDomain(this UserRoleEntity entity)
        {
            if (entity is null) return null!;
            return new UserRole
            {
                UserId = entity.UserId,
                RoleId = entity.RoleId,
                Role = entity.Role?.ToDomain()
            };
        }

        // ===========================
        // Dominio -> Infra
        // ===========================
        public static UserEntity ToEntity(this User domain)
        {
            if (domain is null) return null!;

            return new UserEntity
            {
                Id = domain.Id,
                Username = domain.Username,
                Email = domain.Email,
                PasswordHash = domain.PasswordHash,
                IsActive = domain.IsActive,
                MustChangePassword = domain.MustChangePassword,
                OwnerUserId = domain.OwnerUserId,

                // ⚠️ Importante: NO asignamos Role completo para evitar inserts duplicados de Roles.
                UserRoles = domain.UserRoles?.Select(ur => new UserRoleEntity
                {
                    UserId = ur.UserId,
                    RoleId = ur.RoleId
                    // Role = null  // <- explícitamente no seteamos Role aquí
                }).ToList() ?? new List<UserRoleEntity>()
            };
        }

        public static RoleEntity ToEntity(this Role domain)
        {
            if (domain is null) return null!;
            return new RoleEntity
            {
                Id = domain.Id,
                Name = domain.Name
            };
        }

        // (Opcional)
        public static UserRoleEntity ToEntity(this UserRole domain)
        {
            if (domain is null) return null!;
            return new UserRoleEntity
            {
                UserId = domain.UserId,
                RoleId = domain.RoleId
                // Role = null  // evitamos push de Role para no duplicar
            };
        }
    }
}
