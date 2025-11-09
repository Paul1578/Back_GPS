using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using fletflow.Infrastructure.Persistence.Entities;

namespace fletflow.Infrastructure.Persistence.Configurations
{
    public class RoleConfiguration : IEntityTypeConfiguration<RoleEntity>
    {
        public void Configure(EntityTypeBuilder<RoleEntity> builder)
        {
            builder.ToTable("Roles");
            builder.HasKey(r => r.Id);
            builder.Property(r => r.Name).IsRequired().HasMaxLength(100);
            builder.HasIndex(r => r.Name).IsUnique();

            builder
                .HasMany(r => r.UserRoles)
                .WithOne(ur => ur.Role)
                .HasForeignKey(ur => ur.RoleId);
        }
    }
}
