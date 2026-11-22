using System.Security.Claims;
using fletflow.Application.Users.Commands;
using fletflow.Domain.Auth.Entities;
using fletflow.Domain.Auth.Repositories;
using fletflow.Domain.Fleet.Repositories;
using fletflow.Infrastructure.Persistence;
using fletflow.Infrastructure.Persistence.Context;
using fletflow.Infrastructure.Persistence.Entities;
using fletflow.Infrastructure.Persistence.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

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
        var unitOfWork = new UnitOfWork(
            context, new UserRepository(context), null!, refreshTokens, null!, null!, null!, null!, null!);
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
        public Task RemoveExpiredAsync() => Task.CompletedTask;
        public Task RevokeAllForUserAsync(Guid userId) { RevokeAllCount++; return Task.CompletedTask; }
    }
}
