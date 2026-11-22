using System.Security.Claims;
using fletflow.Application.Fleet.Commands;
using fletflow.Domain.Fleet.Entities;
using fletflow.Domain.Fleet.Exceptions;
using fletflow.Api.Middleware;
using fletflow.Infrastructure.Persistence;
using fletflow.Infrastructure.Persistence.Context;
using fletflow.Infrastructure.Persistence.Entities;
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
        Assert.NotNull(created.CreatedAtUtc);
        Assert.Equal(DateTimeKind.Utc, created.CreatedAtUtc!.Value.Kind);
        Assert.NotNull(persistedRoute!.CreatedAtUtc);
        Assert.Equal(DateTimeKind.Utc, persistedRoute.CreatedAtUtc!.Value.Kind);
        Assert.Equal(created.CreatedAtUtc, persistedRoute.CreatedAtUtc);
        Assert.Equal(vehicle.Id, persistedDriver!.VehicleId);
        Assert.Equal(driver.Id, persistedRoute.DriverId);
        Assert.Equal(vehicle.Id, persistedRoute.VehicleId);
    }

    [Fact]
    public async Task Execute_RejectsVehicleAlreadyAssignedToAnotherDriver_WithoutChanges()
    {
        var ownerId = Guid.NewGuid();
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        var http = new DefaultHttpContext();
        http.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("owner_id", ownerId.ToString())], "test"));
        await using var context = new AppDbContext(options, new HttpContextAccessor { HttpContext = http });
        await context.Database.EnsureCreatedAsync();

        var vehicles = new VehicleRepository(context);
        var drivers = new DriverRepository(context);
        var routes = new RouteRepository(context);
        var vehicle = Vehicle.Create("QA-EXCLUSIVE", "Test", "Van", 2025, null);
        var assignedDriver = Driver.Create("Assigned", "Driver", "QA-ASSIGNED", "0990000001");
        var requestedDriver = Driver.Create("Requested", "Driver", "QA-REQUESTED", "0990000002");
        assignedDriver.AssignToVehicle(vehicle.Id);
        await vehicles.AddAsync(vehicle);
        await drivers.AddAsync(assignedDriver);
        await drivers.AddAsync(requestedDriver);
        await context.SaveChangesAsync();

        var command = new CreateRouteCommand(
            new UnitOfWork(context, null!, null!, null!, null!, vehicles, drivers, routes, null!),
            routes, vehicles, drivers);

        await Assert.ThrowsAsync<BusinessConflictException>(() => command.Execute(
            vehicle.Id,
            requestedDriver.Id,
            "Conflicting route",
            [RoutePoint.Create(-0.2, -78.5, "Origin"), RoutePoint.Create(-0.21, -78.51, "Destination")],
            null,
            null,
            null));

        Assert.Empty(await routes.GetAllAsync());
        Assert.Equal(vehicle.Id, (await drivers.GetByIdAsync(assignedDriver.Id))!.VehicleId);
        Assert.Null((await drivers.GetByIdAsync(requestedDriver.Id))!.VehicleId);
    }

    [Fact]
    public async Task Update_RejectsVehicleAlreadyAssignedToAnotherDriver_WithoutChanges()
    {
        var ownerId = Guid.NewGuid();
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        var http = new DefaultHttpContext();
        http.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("owner_id", ownerId.ToString())], "test"));
        await using var context = new AppDbContext(options, new HttpContextAccessor { HttpContext = http });
        await context.Database.EnsureCreatedAsync();

        var vehicles = new VehicleRepository(context);
        var drivers = new DriverRepository(context);
        var routes = new RouteRepository(context);
        var sourceVehicle = Vehicle.Create("QA-SOURCE", "Test", "Van", 2025, null);
        var assignedVehicle = Vehicle.Create("QA-TARGET", "Test", "Van", 2025, null);
        var routeDriver = Driver.Create("Route", "Driver", "QA-ROUTE-DRIVER", "0990000003");
        var assignedDriver = Driver.Create("Assigned", "Driver", "QA-TARGET-DRIVER", "0990000004");
        await vehicles.AddAsync(sourceVehicle);
        await vehicles.AddAsync(assignedVehicle);
        await drivers.AddAsync(routeDriver);
        await drivers.AddAsync(assignedDriver);
        await context.SaveChangesAsync();

        var uow = new UnitOfWork(context, null!, null!, null!, null!, vehicles, drivers, routes, null!);
        var points = new[]
        {
            RoutePoint.Create(-0.2, -78.5, "Origin"),
            RoutePoint.Create(-0.21, -78.51, "Destination")
        };
        var existing = await new CreateRouteCommand(uow, routes, vehicles, drivers).Execute(
            sourceVehicle.Id, routeDriver.Id, "Existing route", points, null, null, null);
        assignedDriver.AssignToVehicle(assignedVehicle.Id);
        await drivers.UpdateAsync(assignedDriver);
        await context.SaveChangesAsync();

        var command = new UpdateRouteCommand(uow, routes, vehicles, drivers);
        await Assert.ThrowsAsync<BusinessConflictException>(() => command.Execute(
            existing.Id,
            assignedVehicle.Id,
            routeDriver.Id,
            "Conflicting edit",
            points,
            null,
            null,
            null));

        var persistedRoute = await routes.GetByIdAsync(existing.Id);
        Assert.Equal(sourceVehicle.Id, persistedRoute!.VehicleId);
        Assert.Equal(routeDriver.Id, persistedRoute.DriverId);
        Assert.Equal(sourceVehicle.Id, (await drivers.GetByIdAsync(routeDriver.Id))!.VehicleId);
        Assert.Equal(assignedVehicle.Id, (await drivers.GetByIdAsync(assignedDriver.Id))!.VehicleId);
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
            drivers,
            new UserRepository(context));
        var middleware = new ExceptionMiddleware(NullLogger<ExceptionMiddleware>.Instance);
        http.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(http, async _ =>
            await command.Execute(created.Id, RouteStatus.InProgress));

        Assert.Equal(StatusCodes.Status409Conflict, http.Response.StatusCode);
        Assert.Equal(RouteStatus.Pending, (await routes.GetByIdAsync(created.Id))!.Status);
        Assert.Equal(VehicleStatus.Available, (await vehicles.GetByIdAsync(vehicle.Id))!.Status);
    }

    [Fact]
    public async Task ChangeRouteStatus_CancellingPendingRoutePreservesVehicleMaintenanceStatus()
    {
        var ownerId = Guid.NewGuid();
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("owner_id", ownerId.ToString())], "test"))
        };
        await using var context = new AppDbContext(options, new HttpContextAccessor { HttpContext = http });
        await context.Database.EnsureCreatedAsync();

        var vehicles = new VehicleRepository(context);
        var drivers = new DriverRepository(context);
        var routes = new RouteRepository(context);
        var vehicle = Vehicle.Create("QA-PENDING-MAINT", "Test", "Van", 2025, null);
        var driver = Driver.Create("Test", "Driver", "QA-PENDING-MAINT-D", "0990000000");
        await vehicles.AddAsync(vehicle);
        await drivers.AddAsync(driver);
        await context.SaveChangesAsync();

        var unitOfWork = new UnitOfWork(context, null!, null!, null!, null!, vehicles, drivers, routes, null!);
        var route = await new CreateRouteCommand(unitOfWork, routes, vehicles, drivers).Execute(
            vehicle.Id,
            driver.Id,
            "Pending maintenance route",
            [RoutePoint.Create(-0.2, -78.5, "Origin"), RoutePoint.Create(-0.21, -78.51, "Destination")],
            null,
            null,
            null);

        vehicle.SetStatus(VehicleStatus.Maintenance);
        await vehicles.UpdateAsync(vehicle);
        await context.SaveChangesAsync();

        var command = new ChangeRouteStatusCommand(unitOfWork, routes, vehicles, drivers, null!);
        await command.Execute(route.Id, RouteStatus.Cancelled);

        Assert.Equal(RouteStatus.Cancelled, (await routes.GetByIdAsync(route.Id))!.Status);
        Assert.Equal(VehicleStatus.Maintenance, (await vehicles.GetByIdAsync(vehicle.Id))!.Status);
    }

    [Fact]
    public async Task ChangeRouteStatus_RejectsStartingRouteWhenLinkedUserIsInactive_WithoutChangingRouteOrVehicle()
    {
        var ownerId = Guid.NewGuid();
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("owner_id", ownerId.ToString())], "test"))
        };
        await using var context = new AppDbContext(options, new HttpContextAccessor { HttpContext = http });
        await context.Database.EnsureCreatedAsync();

        var vehicles = new VehicleRepository(context);
        var drivers = new DriverRepository(context);
        var routes = new RouteRepository(context);
        var users = new UserRepository(context);
        var linkedUser = new UserEntity
        {
            Id = Guid.NewGuid(), OwnerUserId = ownerId, Username = "linked-driver",
            Email = "linked-driver@example.test", PasswordHash = "test-hash", IsActive = true
        };
        var vehicle = Vehicle.Create("QA-LINKED-USER", "Test", "Van", 2025, null);
        var driver = Driver.Create("Linked", "Driver", "QA-LINKED-USER", "0990000000", linkedUser.Id);
        await context.Users.AddAsync(linkedUser);
        await vehicles.AddAsync(vehicle);
        await drivers.AddAsync(driver);
        await context.SaveChangesAsync();

        var createCommand = new CreateRouteCommand(
            new UnitOfWork(context, users, null!, null!, null!, vehicles, drivers, routes, null!),
            routes, vehicles, drivers);
        var created = await createCommand.Execute(
            vehicle.Id,
            driver.Id,
            "Pending route for linked account",
            [RoutePoint.Create(-0.2, -78.5, "Origin"), RoutePoint.Create(-0.21, -78.51, "Destination")],
            null, null, null);

        linkedUser.IsActive = false;
        await context.SaveChangesAsync();

        var command = new ChangeRouteStatusCommand(
            new UnitOfWork(context, users, null!, null!, null!, vehicles, drivers, routes, null!),
            routes, vehicles, drivers, users);

        await Assert.ThrowsAsync<BusinessConflictException>(() =>
            command.Execute(created.Id, RouteStatus.InProgress));

        Assert.Equal(RouteStatus.Pending, (await routes.GetByIdAsync(created.Id))!.Status);
        Assert.Equal(VehicleStatus.Available, (await vehicles.GetByIdAsync(vehicle.Id))!.Status);
        Assert.False((await users.GetByIdAsync(linkedUser.Id))!.IsActive);
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
        await vehicles.AddAsync(vehicle);
        await drivers.AddAsync(firstDriver);
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
        var second = await create.Execute(vehicle.Id, firstDriver.Id, "Second route", points, null,
            new DateTime(2035, 1, 2, 10, 0, 0, DateTimeKind.Utc),
            new DateTime(2035, 1, 2, 11, 0, 0, DateTimeKind.Utc));

        var command = new ChangeRouteStatusCommand(uow, routes, vehicles, drivers, null!);
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
