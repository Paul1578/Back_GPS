using System.Security.Claims;
using fletflow.Infrastructure.Persistence.Context;
using fletflow.Infrastructure.Persistence.Entities;
using fletflow.Infrastructure.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.IdentityModel.Tokens.Jwt;

namespace BackEnd_Gps.Tests;

public class TenantIsolationTests
{
    [Fact]
    public void AccessTokenIncludesCompanyOwnerClaim()
    {
        var ownerId = Guid.NewGuid();
        var settings = Options.Create(new JwtSettings
        {
            Key = "test-signing-key-must-have-at-least-32-bytes",
            Issuer = "fletflow-test",
            Audience = "fletflow-test",
            ExpireMinutes = 10
        });

        var token = new JwtTokenService(settings).GenerateToken(
            Guid.NewGuid(), "test-user", "test@example.invalid", "Admin", ownerId);
        var parsed = new JwtSecurityTokenHandler().ReadJwtToken(token);

        Assert.Equal(ownerId.ToString(), parsed.Claims.Single(claim => claim.Type == "owner_id").Value);
    }

    [Fact]
    public async Task VehiclesDriversRoutesAndPositionsAreFilteredByOwnerClaim()
    {
        var database = Guid.NewGuid().ToString("N");
        var companyA = Guid.NewGuid();
        var companyB = Guid.NewGuid();

        await AddCompanyFleet(database, companyA, "A");
        await AddCompanyFleet(database, companyB, "B");

        var companyAPlates = await Task.Run(async () =>
        {
            await using var context = CreateContext(database, companyA);
            return await context.Vehicles.Select(vehicle => vehicle.Plate).ToListAsync();
        });
        var companyBPlates = await Task.Run(async () =>
        {
            await using var context = CreateContext(database, companyB);
            return await context.Vehicles.Select(vehicle => vehicle.Plate).ToListAsync();
        });
        var allVehicles = await Task.Run(async () =>
        {
            await using var context = CreateContext(database, companyA);
            return await context.Vehicles.IgnoreQueryFilters().CountAsync();
        });
        var companyARoutes = await Task.Run(async () =>
        {
            await using var context = CreateContext(database, companyA);
            return await context.Routes.Select(route => route.Name).ToListAsync();
        });
        var companyBRoutes = await Task.Run(async () =>
        {
            await using var context = CreateContext(database, companyB);
            return await context.Routes.Select(route => route.Name).ToListAsync();
        });
        var companyADrivers = await Task.Run(async () =>
        {
            await using var context = CreateContext(database, companyA);
            return await context.Drivers.Select(driver => driver.DocumentNumber).ToListAsync();
        });
        var companyBDrivers = await Task.Run(async () =>
        {
            await using var context = CreateContext(database, companyB);
            return await context.Drivers.Select(driver => driver.DocumentNumber).ToListAsync();
        });
        var companyAPositions = await Task.Run(async () =>
        {
            await using var context = CreateContext(database, companyA);
            return await context.RoutePositions.Select(position => position.RouteId).ToListAsync();
        });
        var companyBPositions = await Task.Run(async () =>
        {
            await using var context = CreateContext(database, companyB);
            return await context.RoutePositions.Select(position => position.RouteId).ToListAsync();
        });

        Assert.Equal(new[] { "A-001" }, companyAPlates);
        Assert.Equal(new[] { "B-001" }, companyBPlates);
        Assert.Equal(2, allVehicles);
        Assert.Equal(new[] { "A-route" }, companyARoutes);
        Assert.Equal(new[] { "B-route" }, companyBRoutes);
        Assert.Equal(new[] { "A-driver" }, companyADrivers);
        Assert.Equal(new[] { "B-driver" }, companyBDrivers);
        Assert.Single(companyAPositions);
        Assert.Single(companyBPositions);
        Assert.NotEqual(Assert.Single(companyAPositions), Assert.Single(companyBPositions));
    }

    [Fact]
    public async Task FleetResourcesCannotBeCreatedWithoutOwnerClaim()
    {
        await using var context = CreateContext(Guid.NewGuid().ToString("N"), null);
        context.Vehicles.Add(NewVehicle("NO-OWNER"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync());
    }

    private static AppDbContext CreateContext(string database, Guid? ownerId)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(database)
            .Options;
        var httpContext = new DefaultHttpContext();
        if (ownerId.HasValue)
        {
            httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim("owner_id", ownerId.Value.ToString()) },
                authenticationType: "test"));
        }

        return new AppDbContext(options, new HttpContextAccessor { HttpContext = httpContext });
    }

    private static async Task AddCompanyFleet(string database, Guid ownerId, string prefix)
    {
        await using var context = CreateContext(database, ownerId);
        var vehicle = NewVehicle($"{prefix}-001");
        var driver = new DriverEntity
        {
            Id = Guid.NewGuid(),
            FirstName = $"{prefix} Driver",
            LastName = "Test",
            DocumentNumber = $"{prefix}-driver",
            PhoneNumber = "0990000000",
            IsActive = true,
            VehicleId = vehicle.Id,
            Vehicle = vehicle
        };
        var route = new RouteEntity
        {
            Id = Guid.NewGuid(),
            VehicleId = vehicle.Id,
            Vehicle = vehicle,
            DriverId = driver.Id,
            Driver = driver,
            Name = $"{prefix}-route",
            Origin = "Test origin",
            OriginLat = -0.18,
            OriginLng = -78.46,
            Destination = "Test destination",
            DestinationLat = -0.19,
            DestinationLng = -78.47,
            Status = 0,
            IsActive = true
        };
        context.AddRange(vehicle, driver, route, new RoutePositionEntity
        {
            Id = Guid.NewGuid(),
            RouteId = route.Id,
            Route = route,
            Latitude = -0.185,
            Longitude = -78.465,
            RecordedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();
    }

    private static VehicleEntity NewVehicle(string plate) => new()
    {
        Id = Guid.NewGuid(),
        Plate = plate,
        Brand = "Test",
        Model = "Vehicle",
        Year = 2025,
        IsActive = true,
        Status = 0
    };
}
