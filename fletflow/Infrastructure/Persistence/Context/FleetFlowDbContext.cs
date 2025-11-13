using Microsoft.EntityFrameworkCore;
using fletflow.Infrastructure.Persistence.Entities;
using fletflow.Infrastructure.Persistence.Configurations;

namespace fletflow.Infrastructure.Persistence.Context
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        // --> ¡Asegúrate que sean las Entities del folder Infrastructure/Persistence/Entities!
        public DbSet<UserEntity> Users { get; set; } = default!;
        public DbSet<RoleEntity> Roles { get; set; } = default!;
        public DbSet<UserRoleEntity> UserRoles { get; set; } = default!;
        public DbSet<RefreshTokenEntity> RefreshTokens { get; set; } = default!;
        public DbSet<PasswordResetTokenEntity> PasswordResetTokens { get; set; } = default!;
        public DbSet<VehicleEntity> Vehicles { get; set; } = default!;
        public DbSet<DriverEntity> Drivers { get; set; } = default!;


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfiguration(new UserConfiguration());
            modelBuilder.ApplyConfiguration(new RoleConfiguration());
            modelBuilder.ApplyConfiguration(new UserRoleConfiguration());
            modelBuilder.Entity<RefreshTokenEntity>(b =>
            {
                b.HasIndex(x => x.TokenHash).IsUnique();
                b.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            });
            modelBuilder.Entity<PasswordResetTokenEntity>(b =>
            {
                b.HasIndex(x => x.TokenHash).IsUnique();
                b.HasOne(x => x.User)
                .WithMany() // o colección si la defines
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.ApplyConfiguration(new VehicleConfiguration());
             modelBuilder.ApplyConfiguration(new DriverConfiguration());
            base.OnModelCreating(modelBuilder);
        }
    }
}
