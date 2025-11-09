using fletflow.Infrastructure.Persistence.Context;
using fletflow.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext db)
    {
        await db.Database.MigrateAsync();

        // 1) Asegurar roles base
        var baseRoles = new[] { "Admin", "Manager", "Driver", "User" };
        var existingRoleNames = await db.Roles.Select(r => r.Name).ToListAsync();

        var missingRoles = baseRoles
            .Except(existingRoleNames, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (missingRoles.Count > 0)
        {
            foreach (var r in missingRoles)
                db.Roles.Add(new RoleEntity { Name = r });
            await db.SaveChangesAsync();
        }

        // 2) Asegurar usuario admin
        var adminEmail = "admin@demo.com";
        var admin = await db.Users
            .Include(u => u.UserRoles)
            .FirstOrDefaultAsync(u => u.Email == adminEmail);

        if (admin is null)
        {
            admin = new UserEntity
            {
                Username = "admin",
                Email = adminEmail,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin123!"),
                IsActive = true
            };
            db.Users.Add(admin);
            await db.SaveChangesAsync(); // asegura admin.Id
        }

        // 3) Asegurar relación Admin (usuario) -> Admin (rol)
        var adminRoleId = await db.Roles
            .Where(r => r.Name == "Admin")
            .Select(r => r.Id)
            .FirstAsync(); // existe seguro por (1)

        var hasAdminRole = await db.UserRoles
            .AnyAsync(ur => ur.UserId == admin.Id && ur.RoleId == adminRoleId);

        if (!hasAdminRole)
        {
            db.UserRoles.Add(new UserRoleEntity
            {
                UserId = admin.Id,
                RoleId = adminRoleId
            });
            await db.SaveChangesAsync();
        }
    }
}
