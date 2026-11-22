using System.Security.Claims;
using fletflow.Domain.Auth.Entities;
using fletflow.Infrastructure.Persistence.Context;
using fletflow.Infrastructure.Persistence.Entities;
using fletflow.Infrastructure.Persistence.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace BackEnd_Gps.Tests;

public sealed class PasswordResetTokenRepositoryTests
{
    [Fact]
    public async Task MarkUsedAsync_ConsumesAnActiveTokenOnlyOnce()
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

        var user = new UserEntity
        {
            Id = ownerId,
            Username = "reset-token-owner",
            Email = "reset-token-owner@example.test",
            PasswordHash = "test-hash",
            IsActive = true
        };
        var token = new PasswordResetToken
        {
            UserId = ownerId,
            TokenHash = Guid.NewGuid().ToString("N"),
            ExpiresAt = DateTime.UtcNow.AddMinutes(10)
        };
        context.Users.Add(user);
        context.PasswordResetTokens.Add(new PasswordResetTokenEntity
        {
            Id = token.Id,
            UserId = token.UserId,
            TokenHash = token.TokenHash,
            CreatedAt = token.CreatedAt,
            ExpiresAt = token.ExpiresAt
        });
        await context.SaveChangesAsync();

        var repository = new PasswordResetTokenRepository(context);
        Assert.True(await repository.MarkUsedAsync(token.Id));
        Assert.False(await repository.MarkUsedAsync(token.Id));

        var persisted = await context.PasswordResetTokens.AsNoTracking().SingleAsync(item => item.Id == token.Id);
        Assert.NotNull(persisted.UsedAt);
    }
}
