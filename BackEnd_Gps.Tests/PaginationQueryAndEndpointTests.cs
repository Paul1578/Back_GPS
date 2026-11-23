using System.Reflection;
using System.Security.Claims;
using fletflow.Api.Controllers;
using fletflow.Application.Fleet.Queries;
using fletflow.Application.Shared;
using fletflow.Application.Users.Queries;
using fletflow.Domain.Fleet.Repositories;
using fletflow.Infrastructure.Persistence.Context;
using fletflow.Infrastructure.Persistence.Entities;
using fletflow.Infrastructure.Persistence.Repositories;
using fletflow.Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace BackEnd_Gps.Tests;

public class PaginationQueryAndEndpointTests
{
    [Fact]
    public async Task UsersPage_FiltersTenantRoleActivityAndSearchBeforePaging_InStableOrder()
    {
        var owner = Guid.NewGuid();
        var otherOwner = Guid.NewGuid();
        var database = await CreateDatabase(owner);
        await using var db = database.Context;
        database.Accessor.HttpContext = AuthenticatedContext(owner);
        var memberRole = new RoleEntity { Name = "Manager" };
        var driverRole = new RoleEntity { Name = "Driver" };
        db.Roles.AddRange(memberRole, driverRole);
        var users = new[]
        {
            User(owner, "zoe", "Zoe", "Zulu", true, memberRole),
            User(owner, "amy2", "Amy", "Able", true, memberRole),
            User(owner, "amy1", "Amy", "Able", true, memberRole),
            User(owner, "inactive", "Amy", "Able", false, memberRole),
            User(otherOwner, "foreign", "Amy", "Able", true, memberRole)
        };
        db.Users.AddRange(users);
        db.Users.Add(new UserEntity
        {
            Id = Guid.NewGuid(), Username = "root", FirstName = "Amy", LastName = "Able",
            Email = "root@example.invalid", PasswordHash = "hash", IsActive = true,
            OwnerUserId = null, CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var query = new GetMyTeamUsersQuery(db);
        var firstPage = await query.ExecutePage(owner, page: 1, pageSize: 1, search: "Amy", role: "Manager", isActive: true);
        var secondPage = await query.ExecutePage(owner, page: 2, pageSize: 1, search: "Amy", role: "Manager", isActive: true);
        var emptyPage = await query.ExecutePage(owner, page: 3, pageSize: 1, search: "Amy", role: "Manager", isActive: true);
        var maximumPage = await query.ExecutePage(owner, page: int.MaxValue, pageSize: 100, search: "Amy", role: "Manager", isActive: true);

        Assert.Equal(2, firstPage.TotalCount);
        Assert.Equal(2, firstPage.TotalPages);
        Assert.Equal("amy1", Assert.Single(firstPage.Items).Username);
        Assert.Equal("amy2", Assert.Single(secondPage.Items).Username);
        Assert.Empty(emptyPage.Items);
        Assert.Equal(2, emptyPage.TotalPages);
        Assert.Empty(maximumPage.Items);
        Assert.Equal(int.MaxValue, maximumPage.Page);
    }

    [Fact]
    public async Task DriversPage_PreservesGlobalTenantFilterAndAppliesSearchActivityAndStableOrder()
    {
        var owner = Guid.NewGuid();
        var otherOwner = Guid.NewGuid();
        var database = await CreateDatabase(owner);
        await using var db = database.Context;
        database.Accessor.HttpContext = AuthenticatedContext(owner);
        var foreignDriver = Driver(otherOwner, "Amy", "Able", "A-foreign", true);
        db.Drivers.AddRange(
            Driver(owner, "Zoe", "Zulu", "Z-3", true),
            Driver(owner, "Amy", "Able", "A-2", true),
            Driver(owner, "Amy", "Able", "A-1", true),
            Driver(owner, "Amy", "Able", "A-0", false),
            foreignDriver);
        await db.SaveChangesAsync();
        await db.Drivers.IgnoreQueryFilters()
            .Where(driver => driver.Id == foreignDriver.Id)
            .ExecuteUpdateAsync(update => update.SetProperty(driver => driver.OwnerUserId, otherOwner));

        var repo = new DriverRepository(db);
        var query = new GetDriversQuery(repo, repo);
        var firstPage = await query.ExecutePage(page: 1, pageSize: 1, search: "Able", onlyActive: true);
        var secondPage = await query.ExecutePage(page: 2, pageSize: 1, search: "Able", onlyActive: true);
        var emptyPage = await query.ExecutePage(page: 3, pageSize: 1, search: "Able", onlyActive: true);
        var maximumPage = await query.ExecutePage(page: int.MaxValue, pageSize: 100, search: "Able", onlyActive: true);

        Assert.Equal(2, firstPage.TotalCount);
        Assert.Equal(2, firstPage.TotalPages);
        Assert.Equal("A-1", Assert.Single(firstPage.Items).DocumentNumber);
        Assert.Equal("A-2", Assert.Single(secondPage.Items).DocumentNumber);
        Assert.Empty(emptyPage.Items);
        Assert.Equal(2, emptyPage.TotalPages);
        Assert.Empty(maximumPage.Items);
        Assert.Equal(int.MaxValue, maximumPage.Page);
    }

    [Theory]
    [InlineData(0, 25)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public async Task QueriesRejectInvalidPageLimits(int page, int pageSize)
    {
        var database = await CreateDatabase(Guid.NewGuid());
        await using var db = database.Context;
        database.Accessor.HttpContext = AuthenticatedContext(database.Context.CurrentOwnerUserId ?? Guid.NewGuid());
        var users = new GetMyTeamUsersQuery(db);
        var drivers = new GetDriversQuery(new DriverRepository(db), new DriverRepository(db));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => users.ExecutePage(Guid.NewGuid(), page, pageSize));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => drivers.ExecutePage(page, pageSize));
    }

    [Fact]
    public async Task PagedEndpointsExposeCompatibilityRoutesDefaultsAclAndRejectInvalidBounds()
    {
        Assert.Equal("my-team/page", typeof(UsersController).GetMethod(nameof(UsersController.GetMyTeamPage))!
            .GetCustomAttribute<HttpGetAttribute>()!.Template);
        Assert.Equal("page", typeof(DriversController).GetMethod(nameof(DriversController.GetPage))!
            .GetCustomAttribute<HttpGetAttribute>()!.Template);
        Assert.Equal("Admin", typeof(UsersController).GetCustomAttribute<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>()!.Roles);
        Assert.Equal("Admin,Manager", typeof(DriversController).GetCustomAttribute<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>()!.Roles);
        Assert.Equal(25, PaginationLimits.DefaultPageSize);
        Assert.Equal(100, PaginationLimits.MaximumPageSize);

        var endpointOwner = Guid.NewGuid();
        var database = await CreateDatabase(endpointOwner);
        await using var db = database.Context;
        database.Accessor.HttpContext = AuthenticatedContext(endpointOwner);
        var usersQuery = new GetMyTeamUsersQuery(db);
        var usersController = CreateUsersController(usersQuery);
        var invalidUsers = await usersController.GetMyTeamPage(page: 0);
        Assert.IsType<BadRequestObjectResult>(invalidUsers);
        var defaultUsers = Assert.IsType<OkObjectResult>(await usersController.GetMyTeamPage());
        var userPage = Assert.IsType<PagedResult<global::fletflow.Application.DTOs.Users.UserDto>>(defaultUsers.Value);
        Assert.Equal(25, userPage.PageSize);

        var driversQuery = new GetDriversQuery(new DriverRepository(db), new DriverRepository(db));
        var driversController = CreateDriversController(driversQuery);
        var invalidDrivers = await driversController.GetPage(pageSize: 101);
        Assert.IsType<BadRequestObjectResult>(invalidDrivers);
        var defaultDrivers = Assert.IsType<OkObjectResult>(await driversController.GetPage());
        var driverPage = Assert.IsType<PagedResult<global::fletflow.Application.Fleet.Dtos.DriverDto>>(defaultDrivers.Value);
        Assert.Equal(25, driverPage.PageSize);
    }

    [Fact]
    public async Task EndpointActionsApplyAuthenticatedTenantAndForwardAllFilters()
    {
        var owner = Guid.NewGuid();
        var otherOwner = Guid.NewGuid();
        var database = await CreateDatabase(owner);
        await using var db = database.Context;
        database.Accessor.HttpContext = AuthenticatedContext(owner);

        var manager = new RoleEntity { Name = "Manager" };
        db.Roles.Add(manager);
        db.Users.AddRange(
            User(owner, "target-user", "Needle", "Member", true, manager),
            User(owner, "inactive-user", "Needle", "Member", false, manager),
            User(otherOwner, "foreign-user", "Needle", "Member", true, manager));

        var foreignDriver = Driver(otherOwner, "Needle", "Driver", "FOREIGN", true);
        db.Drivers.AddRange(
            Driver(owner, "Needle", "Driver", "LOCAL", true),
            Driver(owner, "Needle", "Driver", "INACTIVE", false),
            foreignDriver);
        await db.SaveChangesAsync();
        await db.Drivers.IgnoreQueryFilters()
            .Where(driver => driver.Id == foreignDriver.Id)
            .ExecuteUpdateAsync(update => update.SetProperty(driver => driver.OwnerUserId, otherOwner));

        var usersController = CreateUsersController(new GetMyTeamUsersQuery(db), owner);
        var usersResult = Assert.IsType<OkObjectResult>(await usersController.GetMyTeamPage(
            page: 1, pageSize: 25, search: "Needle", role: "Manager", isActive: true));
        var users = Assert.IsType<PagedResult<global::fletflow.Application.DTOs.Users.UserDto>>(usersResult.Value);
        Assert.Equal(1, users.TotalCount);
        Assert.Equal("target-user", Assert.Single(users.Items).Username);

        var driverRepo = new DriverRepository(db);
        var driversController = CreateDriversController(new GetDriversQuery(driverRepo, driverRepo));
        var driversResult = Assert.IsType<OkObjectResult>(await driversController.GetPage(
            page: 1, pageSize: 25, search: "Needle", onlyActive: true));
        var drivers = Assert.IsType<PagedResult<global::fletflow.Application.Fleet.Dtos.DriverDto>>(driversResult.Value);
        Assert.Equal(1, drivers.TotalCount);
        Assert.Equal("LOCAL", Assert.Single(drivers.Items).DocumentNumber);
    }

    private static UsersController CreateUsersController(GetMyTeamUsersQuery query, Guid? owner = null)
    {
        var controller = new UsersController(query, null!, null!, null!, null!, null!, null!,
            Options.Create(new EmailSettings()), null!, NullLogger<UsersController>.Instance);
        controller.ControllerContext = new ControllerContext { HttpContext = AuthenticatedContext(owner ?? Guid.NewGuid()) };
        return controller;
    }

    private static DriversController CreateDriversController(GetDriversQuery query) => new(
        null!, null!, query, null!, null!, null!, null!, null!);

    private static DefaultHttpContext AuthenticatedContext(Guid owner)
    {
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim("owner_id", owner.ToString()) }, "test"));
        return context;
    }

    private static async Task<(AppDbContext Context, HttpContextAccessor Accessor)> CreateDatabase(Guid owner)
    {
        var connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        var accessor = new HttpContextAccessor();
        accessor.HttpContext = AuthenticatedContext(owner);
        var context = new AppDbContext(options, accessor);
        await context.Database.EnsureCreatedAsync();
        Assert.Equal(owner, context.CurrentOwnerUserId);
        return (context, accessor);
    }

    private static UserEntity User(Guid owner, string username, string firstName, string lastName, bool active, RoleEntity role)
    {
        var user = new UserEntity
        {
            Id = Guid.NewGuid(), Username = username, FirstName = firstName, LastName = lastName,
            Email = $"{username}@example.invalid", PasswordHash = "hash", IsActive = active,
            OwnerUserId = owner, CreatedAt = DateTime.UtcNow
        };
        user.UserRoles.Add(new UserRoleEntity { UserId = user.Id, User = user, RoleId = role.Id, Role = role });
        return user;
    }

    private static DriverEntity Driver(Guid owner, string firstName, string lastName, string document, bool active) => new()
    {
        Id = Guid.NewGuid(), FirstName = firstName, LastName = lastName,
        DocumentNumber = document, PhoneNumber = "0990000000", IsActive = active, OwnerUserId = owner
    };
}
