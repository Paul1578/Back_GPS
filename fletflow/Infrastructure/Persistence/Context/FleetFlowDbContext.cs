using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using fletflow.Infrastructure.Persistence.Entities;
using fletflow.Infrastructure.Persistence.Configurations;

namespace fletflow.Infrastructure.Persistence.Context
{
    public class AppDbContext : DbContext
    {
        private readonly IHttpContextAccessor? _httpContextAccessor;

        public AppDbContext(
            DbContextOptions<AppDbContext> options,
            IHttpContextAccessor? httpContextAccessor = null)
            : base(options)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public Guid? CurrentOwnerUserId
        {
            get
            {
                var owner = _httpContextAccessor?.HttpContext?.User.FindFirstValue("owner_id");
                return Guid.TryParse(owner, out var ownerId) ? ownerId : null;
            }
        }

        public DbSet<UserEntity> Users { get; set; } = default!;
        public DbSet<RoleEntity> Roles { get; set; } = default!;
        public DbSet<UserRoleEntity> UserRoles { get; set; } = default!;
        public DbSet<RefreshTokenEntity> RefreshTokens { get; set; } = default!;
        public DbSet<PasswordResetTokenEntity> PasswordResetTokens { get; set; } = default!;
        public DbSet<VehicleEntity> Vehicles { get; set; } = default!;
        public DbSet<DriverEntity> Drivers { get; set; } = default!;
        public DbSet<RouteEntity> Routes { get; set; } = default!;
        public DbSet<RoutePositionEntity> RoutePositions { get; set; } = default!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfiguration(new UserConfiguration());
            modelBuilder.ApplyConfiguration(new RoleConfiguration());
            modelBuilder.ApplyConfiguration(new UserRoleConfiguration());

            modelBuilder.Entity<RefreshTokenEntity>(entity =>
            {
                entity.HasIndex(x => x.TokenHash).IsUnique();
                entity.HasOne(x => x.User)
                      .WithMany()
                      .HasForeignKey(x => x.UserId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<PasswordResetTokenEntity>(entity =>
            {
                entity.HasIndex(x => x.TokenHash).IsUnique();
                entity.HasOne(x => x.User)
                      .WithMany()
                      .HasForeignKey(x => x.UserId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.ApplyConfiguration(new VehicleConfiguration());
            modelBuilder.ApplyConfiguration(new DriverConfiguration());
            modelBuilder.ApplyConfiguration(new RouteConfiguration());
            modelBuilder.ApplyConfiguration(new RoutePositionConfiguration());

            modelBuilder.Entity<VehicleEntity>()
                .HasQueryFilter(entity => CurrentOwnerUserId.HasValue && entity.OwnerUserId == CurrentOwnerUserId);
            modelBuilder.Entity<DriverEntity>()
                .HasQueryFilter(entity => CurrentOwnerUserId.HasValue && entity.OwnerUserId == CurrentOwnerUserId);
            modelBuilder.Entity<RouteEntity>()
                .HasQueryFilter(entity => CurrentOwnerUserId.HasValue && entity.OwnerUserId == CurrentOwnerUserId);
            modelBuilder.Entity<RoutePositionEntity>()
                .HasQueryFilter(entity => CurrentOwnerUserId.HasValue && entity.Route.OwnerUserId == CurrentOwnerUserId);

            base.OnModelCreating(modelBuilder);
        }

        public override int SaveChanges(bool acceptAllChangesOnSuccess)
        {
            StampFleetOwnership();
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }

        public override Task<int> SaveChangesAsync(
            bool acceptAllChangesOnSuccess,
            CancellationToken cancellationToken = default)
        {
            StampFleetOwnership();
            return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }

        private void StampFleetOwnership()
        {
            foreach (var entry in ChangeTracker.Entries()
                         .Where(entry => entry.State == EntityState.Added))
            {
                var ownerProperty = entry.Metadata.FindProperty("OwnerUserId");
                if (ownerProperty is null || entry.Entity is UserEntity)
                    continue;

                var owner = CurrentOwnerUserId
                    ?? throw new InvalidOperationException("No se puede crear un recurso de flota sin propietario autenticado.");
                entry.Property("OwnerUserId").CurrentValue = owner;
            }
        }
    }
}
