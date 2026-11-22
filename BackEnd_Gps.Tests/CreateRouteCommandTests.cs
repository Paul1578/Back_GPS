using System.Security.Claims;
using fletflow.Application.Fleet.Commands;
using fletflow.Domain.Fleet.Entities;
using fletflow.Api.Middleware;
using fletflow.Infrastructure.Persistence;
using fletflow.Infrastructure.Persistence.Context;
using fletflow.Infrastructure.Persistence.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;

namespace BackEnd_Gps.Tests;

public class CreateRouteCommandTests
{
    [Fact]
    public async Task Execute_PersistsRouteAndDriverVehicleAssignmentTogether()
    {
        var ownerId = Guid.NewGuid();
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        var http = new DefaultHttpContext();
        http.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("owner_id", ownerId.ToString())], "test"));
        await using var context = new AppDbContext(options, new HttpContextAccessor { HttpContext = http });
        await context.Database.EnsureCreatedAsync();

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

    [Fact]
    public async Task ChangeRouteStatus_RejectsStartingPendingRouteWhenAssignedDriverIsInactive()
    {
        var ownerId = Guid.NewGuid();
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        var http = new DefaultHttpContext();
        http.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("owner_id", ownerId.ToString())], "test"));
        await using var context = new AppDbContext(options, new HttpContextAccessor { HttpContext = http });
        await context.Database.EnsureCreatedAsync();

        var vehicles = new VehicleRepository(context);
        var drivers = new DriverRepository(context);
        var routes = new RouteRepository(context);
        var vehicle = Vehicle.Create("QA-START", "Test", "Van", 2025, null);
        var driver = Driver.Create("Test", "Driver", "QA-INACTIVE-DRIVER", "0990000000");
        await vehicles.AddAsync(vehicle);
        await drivers.AddAsync(driver);
        await context.SaveChangesAsync();

        var createCommand = new CreateRouteCommand(
            new UnitOfWork(context, null!, null!, null!, null!, vehicles, drivers, routes, null!),
            routes,
            vehicles,
            drivers);
        var created = await createCommand.Execute(
            vehicle.Id,
            driver.Id,
            "Pending route",
            [RoutePoint.Create(-0.2, -78.5, "Origen"), RoutePoint.Create(-0.21, -78.51, "Destino")],
            null,
            null,
            null);

        driver.Deactivate();
        driver.UnassignVehicle();
        await drivers.UpdateAsync(driver);
        await context.SaveChangesAsync();

        var command = new ChangeRouteStatusCommand(
            new UnitOfWork(context, null!, null!, null!, null!, vehicles, drivers, routes, null!),
            routes,
            vehicles,
            drivers);
        var middleware = new ExceptionMiddleware(NullLogger<ExceptionMiddleware>.Instance);
        http.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(http, async _ =>
            await command.Execute(created.Id, RouteStatus.InProgress));

        Assert.Equal(StatusCodes.Status409Conflict, http.Response.StatusCode);
        Assert.Equal(RouteStatus.Pending, (await routes.GetByIdAsync(created.Id))!.Status);
        Assert.Equal(VehicleStatus.Available, (await vehicles.GetByIdAsync(vehicle.Id))!.Status);
    }

    [Fact]
    public async Task ChangeRouteStatus_RejectsSecondInProgressRouteForTheSameVehicle()
    {
        var ownerId = Guid.NewGuid();
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        var http = new DefaultHttpContext();
        http.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("owner_id", ownerId.ToString())], "test"));
        await using var context = new AppDbContext(options, new HttpContextAccessor { HttpContext = http });
        await context.Database.EnsureCreatedAsync();

        var vehicles = new VehicleRepository(context);
        var drivers = new DriverRepository(context);
        var routes = new RouteRepository(context);
        var uow = new UnitOfWork(context, null!, null!, null!, null!, vehicles, drivers, routes, null!);
        var vehicle = Vehicle.Create("QA-ONE-ROUTE", "Test", "Van", 2025, null);
        var firstDriver = Driver.Create("First", "Driver", "QA-FIRST", "0990000001");
        var secondDriver = Driver.Create("Second", "Driver", "QA-SECOND", "0990000002");
        await vehicles.AddAsync(vehicle);
        await drivers.AddAsync(firstDriver);
        await drivers.AddAsync(secondDriver);
        await context.SaveChangesAsync();

        var create = new CreateRouteCommand(uow, routes, vehicles, drivers);
        var points = new[]
        {
            RoutePoint.Create(-0.2, -78.5, "Origen"),
            RoutePoint.Create(-0.21, -78.51, "Destino")
        };
        var first = await create.Execute(vehicle.Id, firstDriver.Id, "First route", points, null,
            new DateTime(2035, 1, 1, 10, 0, 0, DateTimeKind.Utc),
            new DateTime(2035, 1, 1, 11, 0, 0, DateTimeKind.Utc));
        var second = await create.Execute(vehicle.Id, secondDriver.Id, "Second route", points, null,
            new DateTime(2035, 1, 2, 10, 0, 0, DateTimeKind.Utc),
            new DateTime(2035, 1, 2, 11, 0, 0, DateTimeKind.Utc));

        var command = new ChangeRouteStatusCommand(uow, routes, vehicles, drivers);
        await command.Execute(first.Id, RouteStatus.InProgress);

        var middleware = new ExceptionMiddleware(NullLogger<ExceptionMiddleware>.Instance);
        http.Response.Body = new MemoryStream();
        await middleware.InvokeAsync(http, async _ =>
            await command.Execute(second.Id, RouteStatus.InProgress));

        Assert.Equal(StatusCodes.Status409Conflict, http.Response.StatusCode);
        Assert.Equal(RouteStatus.InProgress, (await routes.GetByIdAsync(first.Id))!.Status);
        Assert.Equal(RouteStatus.Pending, (await routes.GetByIdAsync(second.Id))!.Status);
        Assert.Equal(VehicleStatus.OnRoute, (await vehicles.GetByIdAsync(vehicle.Id))!.Status);
    }
}
