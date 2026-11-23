using System.Security.Claims;
using fletflow.Application.Users.Commands;
using fletflow.Domain.Auth.Entities;
using fletflow.Domain.Auth.Repositories;
using fletflow.Domain.Fleet.Entities;
using fletflow.Domain.Fleet.Exceptions;
using fletflow.Domain.Fleet.Repositories;
using fletflow.Infrastructure.Persistence;
using fletflow.Infrastructure.Persistence.Context;
using fletflow.Infrastructure.Persistence.Entities;
using fletflow.Infrastructure.Persistence.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;

namespace BackEnd_Gps.Tests;

public sealed class UpdateUserCommandTests
{
    [Fact]
    public async Task Execute_ChangesTokenVersionOnDeactivateAndReactivate()
    {
        var ownerId = Guid.NewGuid();
        var user = new UserEntity
        {
            Id = Guid.NewGuid(), OwnerUserId = ownerId, Username = "member",
            Email = "member@example.test", PasswordHash = "test-hash", IsActive = true
        };
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"user-status-{Guid.NewGuid()}").Options;
        await using var context = CreateContext(options, ownerId);
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var refreshTokens = new RecordingRefreshTokenRepository();
        var vehicles = new VehicleRepository(context);
        var drivers = new DriverRepository(context);
        var routes = new RouteRepository(context);
        var unitOfWork = new UnitOfWork(
            context, new UserRepository(context), null!, refreshTokens, null!, vehicles, drivers, routes, null!);
        var command = new UpdateUserCommand(unitOfWork, context);

        await command.Execute(user.Id, false);
        Assert.False(user.IsActive);
        Assert.Equal(1, user.TokenVersion);
        Assert.Equal(1, refreshTokens.RevokeAllCount);

        await command.Execute(user.Id, true);
        Assert.True(user.IsActive);
        Assert.Equal(2, user.TokenVersion);
        Assert.Equal(1, refreshTokens.RevokeAllCount);

        await command.Execute(user.Id, true);
        Assert.Equal(2, user.TokenVersion);
        Assert.Equal(2, await context.Users.Where(item => item.Id == user.Id)
            .Select(item => item.TokenVersion).SingleAsync());
    }

    [Fact]
    public async Task Execute_CannotDeactivateLinkedDriverAccountWhileRouteIsInProgress()
    {
        var ownerId = Guid.NewGuid();
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        await using var context = CreateContext(options, ownerId);
        await context.Database.EnsureCreatedAsync();

        var user = new UserEntity
        {
            Id = Guid.NewGuid(), OwnerUserId = ownerId, Username = "active-driver",
            Email = "active-driver@example.test", PasswordHash = "test-hash", IsActive = true
        };
        var vehicles = new VehicleRepository(context);
        var drivers = new DriverRepository(context);
        var routes = new RouteRepository(context);
        var vehicle = Vehicle.Create("QA-USER-ACTIVE-ROUTE", "Test", "Van", 2025, null);
        vehicle.SetStatus(VehicleStatus.OnRoute);
        var driver = Driver.Create("Active", "Driver", "QA-ACTIVE-ROUTE", "0990000000", user.Id);
        driver.AssignToVehicle(vehicle.Id);
        var origin = RoutePoint.Create(-0.2, -78.5, "Origin");
        var destination = RoutePoint.Create(-0.21, -78.51, "Destination");
        var route = RouteE.Create(vehicle.Id, driver.Id, "In progress route", origin, destination,
            [origin, destination], null, null, null);
        route.ChangeStatus(RouteStatus.InProgress);

        await context.Users.AddAsync(user);
        await vehicles.AddAsync(vehicle);
        await drivers.AddAsync(driver);
        await routes.AddAsync(route);
        await context.SaveChangesAsync();

        var refreshTokens = new RecordingRefreshTokenRepository();
        var unitOfWork = new UnitOfWork(context, new UserRepository(context), null!, refreshTokens,
            null!, vehicles, drivers, routes, null!);
        var command = new UpdateUserCommand(unitOfWork, context);

        await Assert.ThrowsAsync<BusinessConflictException>(() => command.Execute(user.Id, false));

        Assert.True((await context.Users.SingleAsync(item => item.Id == user.Id)).IsActive);
        Assert.Equal(RouteStatus.InProgress, (await routes.GetByIdAsync(route.Id))!.Status);
        Assert.Equal(VehicleStatus.OnRoute, (await vehicles.GetByIdAsync(vehicle.Id))!.Status);
        Assert.Equal(0, refreshTokens.RevokeAllCount);
    }

    [Fact]
    public async Task Execute_CannotDeactivateLinkedDriverAccountWhileRouteIsPending()
    {
        var ownerId = Guid.NewGuid();
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        await using var context = CreateContext(options, ownerId);
        await context.Database.EnsureCreatedAsync();

        var user = new UserEntity
        {
            Id = Guid.NewGuid(), OwnerUserId = ownerId, Username = "pending-driver",
            Email = "pending-driver@example.test", PasswordHash = "test-hash", IsActive = true
        };
        var vehicles = new VehicleRepository(context);
        var drivers = new DriverRepository(context);
        var routes = new RouteRepository(context);
        var vehicle = Vehicle.Create("QA-USER-PENDING-ROUTE", "Test", "Van", 2025, null);
        var driver = Driver.Create("Pending", "Driver", "QA-PENDING-ROUTE", "0990000000", user.Id);
        driver.AssignToVehicle(vehicle.Id);
        var origin = RoutePoint.Create(-0.2, -78.5, "Origin");
        var destination = RoutePoint.Create(-0.21, -78.51, "Destination");
        var route = RouteE.Create(vehicle.Id, driver.Id, "Pending route", origin, destination,
            [origin, destination], null, null, null);

        await context.Users.AddAsync(user);
        await vehicles.AddAsync(vehicle);
        await drivers.AddAsync(driver);
        await routes.AddAsync(route);
        await context.SaveChangesAsync();

        var refreshTokens = new RecordingRefreshTokenRepository();
        var unitOfWork = new UnitOfWork(context, new UserRepository(context), null!, refreshTokens,
            null!, vehicles, drivers, routes, null!);
        var command = new UpdateUserCommand(unitOfWork, context);

        await Assert.ThrowsAsync<BusinessConflictException>(() => command.Execute(user.Id, false));

        Assert.True((await context.Users.SingleAsync(item => item.Id == user.Id)).IsActive);
        Assert.True((await drivers.GetByIdAsync(driver.Id))!.IsActive);
        Assert.Equal(vehicle.Id, (await drivers.GetByIdAsync(driver.Id))!.VehicleId);
        Assert.Equal(RouteStatus.Pending, (await routes.GetByIdAsync(route.Id))!.Status);
        Assert.Equal(VehicleStatus.Available, (await vehicles.GetByIdAsync(vehicle.Id))!.Status);
        Assert.Equal(0, refreshTokens.RevokeAllCount);
    }

    private static AppDbContext CreateContext(DbContextOptions<AppDbContext> options, Guid ownerId)
    {
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim("owner_id", ownerId.ToString())], "test"))
        };
        return new AppDbContext(options, new HttpContextAccessor { HttpContext = httpContext });
    }

    private sealed class RecordingRefreshTokenRepository : IRefreshTokenRepository
    {
        public int RevokeAllCount { get; private set; }
        public Task AddAsync(RefreshToken token) => Task.CompletedTask;
        public Task<RefreshToken?> GetByTokenAsync(string rawToken) => Task.FromResult<RefreshToken?>(null);
        public Task RevokeAsync(Guid id, string? replacedByTokenHash = null) => Task.CompletedTask;
        public Task<bool> TryRotateAsync(Guid currentTokenId, RefreshToken replacement) => Task.FromResult(false);
        public Task RemoveExpiredAsync() => Task.CompletedTask;
        public Task RevokeAllForUserAsync(Guid userId) { RevokeAllCount++; return Task.CompletedTask; }
    }
}
