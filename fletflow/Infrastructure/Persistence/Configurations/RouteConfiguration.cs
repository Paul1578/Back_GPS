using fletflow.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace fletflow.Infrastructure.Persistence.Configurations
{
    public class RouteConfiguration : IEntityTypeConfiguration<RouteEntity>
    {
        public void Configure(EntityTypeBuilder<RouteEntity> builder)
        {
            builder.ToTable("Routes");

            builder.HasKey(r => r.Id);
            builder.Property(r => r.Name)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(r => r.Origin)
                .IsRequired()
                .HasMaxLength(200);

            builder.Property(r => r.OriginLat)
                .IsRequired()
                .HasDefaultValue(0d);

            builder.Property(r => r.OriginLng)
                .IsRequired()
                .HasDefaultValue(0d);

            builder.Property(r => r.Destination)
                .IsRequired()
                .HasMaxLength(200);

            builder.Property(r => r.DestinationLat)
                .IsRequired()
                .HasDefaultValue(0d);

            builder.Property(r => r.DestinationLng)
                .IsRequired()
                .HasDefaultValue(0d);

            builder.Property(r => r.CargoDescription)
                .HasMaxLength(1000);

            builder.Property(r => r.Status)
                .IsRequired();

            builder.Property(r => r.IsActive)
                .IsRequired();

            builder.HasOne(r => r.Vehicle)
                .WithMany()
                .HasForeignKey(r => r.VehicleId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(r => r.Driver)
                .WithMany()
                .HasForeignKey(r => r.DriverId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Property(r => r.PointsJson)
                .IsRequired()
                .HasColumnType("longtext")
                .HasDefaultValue("[]");
        }
    }
}
