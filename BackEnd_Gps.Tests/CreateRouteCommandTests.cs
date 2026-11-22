using System.Security.Claims;
using fletflow.Application.Fleet.Commands;
using fletflow.Domain.Fleet.Entities;
using fletflow.Infrastructure.Persistence;
using fletflow.Infrastructure.Persistence.Context;
using fletflow.Infrastructure.Persistence.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace BackEnd_Gps.Tests;

public class CreateRouteCommandTests
{
    [Fact]
    public async Task Execute_PersistsRouteAndDriverVehicleAssignmentTogether()
    {
        var ownerId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var http = new DefaultHttpContext();
        http.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("owner_id", ownerId.ToString())], "test"));
        await using var context = new AppDbContext(options, new HttpContextAccessor { HttpContext = http });

        var vehicles = new VehicleRepository(context);
        var drivers = new DriverRepository(context);
        var routes = new RouteRepository(context);
        var vehicle = Vehicle.Create("QA-ROUTE", "Test", "Van", 2025, null);
        var driver = Driver.Create("Test", "Driver", "QA-DRIVER", "0990000000");
        await vehicles.AddAsync(vehicle);
        await drivers.AddAsync(driver);
        await context.SaveChangesAsync();

        var uow = new UnitOfWork(context, null!, null!, null!, null!, vehicles, drivers, routes, null!);
        var command = new CreateRouteCommand(uow, routes, vehicles, drivers);
        var points = new[]
        {
            RoutePoint.Create(-0.2, -78.5, "Origen"),
            RoutePoint.Create(-0.21, -78.51, "Destino")
        };

        var created = await command.Execute(vehicle.Id, driver.Id, "QA route", points, null, null, null);

        var persistedDriver = await drivers.GetByIdAsync(driver.Id);
        var persistedRoute = await routes.GetByIdAsync(created.Id);
        Assert.Equal(vehicle.Id, persistedDriver!.VehicleId);
        Assert.Equal(driver.Id, persistedRoute!.DriverId);
        Assert.Equal(vehicle.Id, persistedRoute.VehicleId);
    }
}
