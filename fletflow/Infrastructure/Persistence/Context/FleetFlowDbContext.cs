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

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfiguration(new UserConfiguration());
            modelBuilder.ApplyConfiguration(new RoleConfiguration());
            modelBuilder.ApplyConfiguration(new UserRoleConfiguration());

            base.OnModelCreating(modelBuilder);
        }
    }
}
