using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using fletflow.Infrastructure.Persistence.Entities;

namespace fletflow.Infrastructure.Persistence.Configurations
{
    public class UserRoleConfiguration : IEntityTypeConfiguration<UserRoleEntity>
    {
        public void Configure(EntityTypeBuilder<UserRoleEntity> builder)
        {
            builder.ToTable("UserRoles");
            builder.HasKey(ur => new { ur.UserId, ur.RoleId });

            builder
                .HasOne(ur => ur.User)
                .WithMany(u => u.UserRoles)
                .HasForeignKey(ur => ur.UserId);

            builder
                .HasOne(ur => ur.Role)
                .WithMany(r => r.UserRoles)
                .HasForeignKey(ur => ur.RoleId);
        }
    }
}
