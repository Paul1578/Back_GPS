using fletflow.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace fletflow.Infrastructure.Persistence.Configurations
{
    public class RoutePositionConfiguration : IEntityTypeConfiguration<RoutePositionEntity>
    {
        public void Configure(EntityTypeBuilder<RoutePositionEntity> builder)
        {
            builder.ToTable("RoutePositions");

            builder.HasKey(rp => rp.Id);

            builder.Property(rp => rp.Latitude)
                .IsRequired();

            builder.Property(rp => rp.Longitude)
                .IsRequired();

            builder.Property(rp => rp.RecordedAt)
                .IsRequired();

            builder.HasOne(rp => rp.Route)
                .WithMany() // si luego quieres navegación inversa, podemos cambiar esto
                .HasForeignKey(rp => rp.RouteId)
                .OnDelete(DeleteBehavior.Cascade);

            // Índice para consultas por ruta y tiempo
            builder.HasIndex(rp => new { rp.RouteId, rp.RecordedAt });
        }
    }
}
