using fletflow.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace fletflow.Infrastructure.Persistence.Configurations
{
    public class DriverConfiguration : IEntityTypeConfiguration<DriverEntity>
    {
        public void Configure(EntityTypeBuilder<DriverEntity> builder)
        {
            builder.ToTable("Drivers");

            builder.HasKey(d => d.Id);

            builder.Property(d => d.FirstName)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(d => d.LastName)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(d => d.DocumentNumber)
                .IsRequired()
                .HasMaxLength(20);

            builder.HasIndex(d => new { d.OwnerUserId, d.DocumentNumber })
                .IsUnique();

            builder.Property(d => d.PhoneNumber)
                .IsRequired()
                .HasMaxLength(20);

            builder.Property(d => d.IsActive)
                .IsRequired();

            // 👇 relación opcional 1–N: un vehículo puede tener muchos drivers asignados en distintos momentos,
            // pero en la práctica para MVP vamos a usar solo uno "actual".
            builder.HasOne(d => d.Vehicle)
                .WithMany() // si luego quieres navegaci��n inversa, se puede cambiar
                .HasForeignKey(d => d.VehicleId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Property(d => d.UserId)
                .IsRequired(false);

            builder.HasOne<UserEntity>()
                .WithMany()
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(d => d.UserId)
                .IsUnique();
        }

    }
}
