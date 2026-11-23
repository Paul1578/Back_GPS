using fletflow.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BackEnd_Gps.Tests;

public class RoleSeederTests
{
    [Fact]
    public async Task EnsureBaseRoles_CreatesAllSupportedRolesAndIsIdempotent()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"roles-{Guid.NewGuid()}")
            .Options;

        await using var db = new AppDbContext(options);
        await DbSeeder.EnsureBaseRolesAsync(db);
        await DbSeeder.EnsureBaseRolesAsync(db);

        var roles = await db.Roles.Select(role => role.Name).ToListAsync();
        Assert.Equal(5, roles.Count);
        Assert.Contains("Admin", roles);
        Assert.Contains("Driver", roles);
        Assert.Contains("Logistics", roles);
        Assert.Contains("Manager", roles);
        Assert.Contains("User", roles);
    }
}
