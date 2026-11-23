using fletflow.Application.Auth.Commands;
using fletflow.Domain.Auth.Entities;
using fletflow.Domain.Auth.Repositories;
using fletflow.Domain.Fleet.Repositories;
using fletflow.Infrastructure.Persistence.Contracts;

namespace BackEnd_Gps.Tests;

public class LoginUserCommandTests
{
    [Fact]
    public async Task Execute_UsesSameUnauthorizedErrorForUnknownAndInvalidAccounts()
    {
        var expected = "Correo o contraseña incorrectos.";
        var unknown = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            CreateCommand(null).Execute("missing@example.test", "bad-password"));

        var user = new User
        {
            Email = "known@example.test",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("correct-password"),
        };
        var wrongPassword = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            CreateCommand(user).Execute(user.Email, "bad-password"));
        user.IsActive = false;
        var inactive = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            CreateCommand(user).Execute(user.Email, "correct-password"));
        user.IsActive = true;
        user.MustChangePassword = true;
        var pendingActivation = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            CreateCommand(user).Execute(user.Email, "correct-password"));

        Assert.Equal(expected, unknown.Message);
        Assert.Equal(expected, wrongPassword.Message);
        Assert.Equal(expected, inactive.Message);
        Assert.Equal(expected, pendingActivation.Message);
    }

    [Fact]
    public async Task Execute_ReturnsActiveUserWithValidCredentials()
    {
        var user = new User
        {
            Email = "known@example.test",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("correct-password"),
        };

        var result = await CreateCommand(user).Execute(user.Email, "correct-password");

        Assert.Same(user, result);
    }

    [Fact]
    public async Task Execute_AllowsLegacyPasswordShorterThanCurrentMinimum()
    {
        const string legacyPassword = "Old123";
        var user = new User
        {
            Email = "legacy-password@example.test",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(legacyPassword),
        };

        var result = await CreateCommand(user).Execute(user.Email, legacyPassword);

        Assert.Same(user, result);
    }

    private static LoginUserCommand CreateCommand(User? user) =>
        new(new TestUnitOfWork(new TestUserRepository(user)));

    private sealed class TestUserRepository(User? user) : IUserRepository
    {
        public Task<User?> GetByEmailAsync(string email) =>
            Task.FromResult(user?.Email.Equals(email, StringComparison.OrdinalIgnoreCase) == true ? user : null);
        public Task<User?> GetByIdAsync(Guid id) => Task.FromResult<User?>(null);
        public Task AddAsync(User value) => Task.CompletedTask;
        public Task SaveChangesAsync() => Task.CompletedTask;
        public Task<bool> HasRoleAsync(Guid userId, Guid roleId) => Task.FromResult(false);
        public Task AssignRoleAsync(Guid userId, Guid roleId) => Task.CompletedTask;
        public Task RemoveRoleAsync(Guid userId, Guid roleId) => Task.CompletedTask;
        public Task UpdatePasswordHashAsync(Guid userId, string newPasswordHash) => Task.CompletedTask;
        public Task UpdatePasswordAndClearFlagAsync(Guid userId, string newPasswordHash, bool clearMustChangePassword) => Task.CompletedTask;
        public Task IncrementTokenVersionAsync(Guid userId) => Task.CompletedTask;
        public Task<List<User>> GetByOwnerAsync(Guid ownerUserId) => Task.FromResult(new List<User>());
    }

    private sealed class TestUnitOfWork(IUserRepository users) : IUnitOfWork
    {
        public IUserRepository Users { get; } = users;
        public IRoleRepository Roles => null!;
        public IRefreshTokenRepository RefreshTokens => null!;
        public IPasswordResetTokenRepository PasswordResetTokens => null!;
        public IVehicleRepository Vehicles => null!;
        public IDriverRepository Drivers => null!;
        public IRouteRepository Routes => null!;
        public IRoutePositionRepository RoutePositions => null!;
        public Task<int> CommitAsync() => Task.FromResult(0);
        public void Rollback() { }
    }
}
