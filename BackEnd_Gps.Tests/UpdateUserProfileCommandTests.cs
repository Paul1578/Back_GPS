using System.Security.Claims;
using fletflow.Application.Users.Commands;
using fletflow.Infrastructure.Persistence;
using fletflow.Infrastructure.Persistence.Context;
using fletflow.Infrastructure.Persistence.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace BackEnd_Gps.Tests;

public sealed class UpdateUserProfileCommandTests
{
    [Fact]
    public async Task Execute_UpdatesProfileAndLinkedDriverWithinCurrentTenant()
    {
        var ownerId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"user-profile-{Guid.NewGuid()}")
            .Options;
        await using var context = CreateContext(options, ownerId);
        var user = new UserEntity
        {
            Id = userId,
            OwnerUserId = ownerId,
            Username = "driver",
            Email = "driver@example.test",
            PasswordHash = "test-hash",
            FirstName = "Before",
            LastName = "Name"
        };
        var driver = new DriverEntity
        {
            Id = Guid.NewGuid(),
            OwnerUserId = ownerId,
            UserId = userId,
            FirstName = "Before",
            LastName = "Name",
            DocumentNumber = "DOC-001",
            PhoneNumber = "0990000000",
            IsActive = true
        };
        context.Users.Add(user);
        context.Drivers.Add(driver);
        await context.SaveChangesAsync();

        var command = new UpdateUserProfileCommand(context, new UnitOfWork(
            context, null!, null!, null!, null!, null!, null!, null!, null!));
        await command.Execute(userId, "  driver.updated  ", "  Ana  ", "  Pérez  ");

        Assert.Equal("driver.updated", user.Username);
        Assert.Equal("Ana", user.FirstName);
        Assert.Equal("Pérez", user.LastName);
        Assert.Equal("Ana", driver.FirstName);
        Assert.Equal("Pérez", driver.LastName);
    }

    [Fact]
    public async Task Execute_RejectsUserOutsideCurrentTenant()
    {
        var currentOwnerId = Guid.NewGuid();
        var otherOwnerId = Guid.NewGuid();
        var foreignUser = new UserEntity
        {
            Id = Guid.NewGuid(),
            OwnerUserId = otherOwnerId,
            Username = "foreign",
            Email = "foreign@example.test",
            PasswordHash = "test-hash",
            FirstName = "Foreign",
            LastName = "User"
        };
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"user-profile-owner-{Guid.NewGuid()}")
            .Options;
        await using var context = CreateContext(options, currentOwnerId);
        context.Users.Add(foreignUser);
        await context.SaveChangesAsync();

        var command = new UpdateUserProfileCommand(context, new UnitOfWork(
            context, null!, null!, null!, null!, null!, null!, null!, null!));
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            command.Execute(foreignUser.Id, "changed", "Changed", "User"));

        Assert.Equal("foreign", foreignUser.Username);
        Assert.Equal("Foreign", foreignUser.FirstName);
        Assert.Equal("User", foreignUser.LastName);
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
}
