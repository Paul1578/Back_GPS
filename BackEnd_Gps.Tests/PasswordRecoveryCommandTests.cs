using fletflow.Application.Auth.Commands.ActivateAccount;
using fletflow.Application.Auth.Commands.ResetPassword;
using fletflow.Domain.Auth.Entities;
using fletflow.Domain.Auth.Repositories;
using fletflow.Domain.Fleet.Repositories;
using fletflow.Infrastructure.Persistence.Contracts;
using fletflow.Infrastructure.Security.Hashing;

namespace BackEnd_Gps.Tests;

public class PasswordRecoveryCommandTests
{
    [Fact]
    public async Task ResetPassword_ChangesPasswordConsumesTokenAndRevokesSessions()
    {
        var scenario = new RecoveryScenario();
        var command = new ResetPasswordCommand(
            scenario.Tokens, scenario.Users, scenario.RefreshTokens, scenario, new PasswordHasher());

        await command.ExecuteAsync(scenario.RawToken, "NewPass123", "NewPass123");

        Assert.True(new PasswordHasher().Verify("NewPass123", scenario.User.PasswordHash));
        Assert.NotEqual("old-hash", scenario.User.PasswordHash);
        Assert.NotNull(scenario.Token.UsedAt);
        Assert.Equal(1, scenario.RefreshTokens.RevokeAllCount);
        Assert.Equal(1, scenario.CommitCount);
    }

    [Fact]
    public async Task ResetPassword_RejectsConfirmationMismatchWithoutChangingState()
    {
        var scenario = new RecoveryScenario();
        var command = new ResetPasswordCommand(
            scenario.Tokens, scenario.Users, scenario.RefreshTokens, scenario, new PasswordHasher());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            command.ExecuteAsync(scenario.RawToken, "NewPass123", "Different123"));

        Assert.Equal("old-hash", scenario.User.PasswordHash);
        Assert.Null(scenario.Token.UsedAt);
        Assert.Equal(0, scenario.RefreshTokens.RevokeAllCount);
        Assert.Equal(0, scenario.CommitCount);
    }

    [Fact]
    public async Task ResetPassword_RejectsUsedToken()
    {
        var scenario = new RecoveryScenario();
        scenario.Token.UsedAt = DateTime.UtcNow;
        var command = new ResetPasswordCommand(
            scenario.Tokens, scenario.Users, scenario.RefreshTokens, scenario, new PasswordHasher());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            command.ExecuteAsync(scenario.RawToken, "NewPass123", "NewPass123"));

        Assert.Equal("old-hash", scenario.User.PasswordHash);
        Assert.Equal(0, scenario.CommitCount);
    }

    [Fact]
    public async Task ActivateAccount_ChangesPasswordClearsFlagConsumesTokenAndRevokesSessions()
    {
        var scenario = new RecoveryScenario();
        scenario.User.MustChangePassword = true;
        var command = new ActivateAccountCommand(scenario.Tokens, scenario.Users, scenario, new PasswordHasher());

        await command.ExecuteAsync(scenario.RawToken, "Welcome123", "Welcome123");

        Assert.True(new PasswordHasher().Verify("Welcome123", scenario.User.PasswordHash));
        Assert.False(scenario.User.MustChangePassword);
        Assert.NotNull(scenario.Token.UsedAt);
        Assert.Equal(1, scenario.RefreshTokens.RevokeAllCount);
        Assert.Equal(1, scenario.CommitCount);
    }

    private sealed class RecoveryScenario : IUnitOfWork
    {
        public string RawToken { get; } = "recovery-token-for-test";
        public User User { get; } = new() { Email = "recovery@example.test", PasswordHash = "old-hash" };
        public PasswordResetToken Token { get; }
        public UserRepository Users { get; }
        public TokenRepository Tokens { get; }
        public RefreshTokenRepository RefreshTokens { get; } = new();
        public int CommitCount { get; private set; }

        public RecoveryScenario()
        {
            Token = new PasswordResetToken
            {
                UserId = User.Id,
                TokenHash = TokenHashing.Sha256(RawToken),
                ExpiresAt = DateTime.UtcNow.AddMinutes(10)
            };
            Users = new UserRepository(User);
            Tokens = new TokenRepository(Token);
        }

        IUserRepository IUnitOfWork.Users => Users;
        IRoleRepository IUnitOfWork.Roles => throw new NotSupportedException();
        IRefreshTokenRepository IUnitOfWork.RefreshTokens => RefreshTokens;
        IPasswordResetTokenRepository IUnitOfWork.PasswordResetTokens => Tokens;
        IVehicleRepository IUnitOfWork.Vehicles => throw new NotSupportedException();
        IDriverRepository IUnitOfWork.Drivers => throw new NotSupportedException();
        IRouteRepository IUnitOfWork.Routes => throw new NotSupportedException();
        IRoutePositionRepository IUnitOfWork.RoutePositions => throw new NotSupportedException();
        public Task<int> CommitAsync() { CommitCount++; return Task.FromResult(1); }
        public void Rollback() { }

        public sealed class UserRepository(User user) : IUserRepository
        {
            public Task<User?> GetByEmailAsync(string email) => Task.FromResult<User?>(null);
            public Task<User?> GetByIdAsync(Guid id) => Task.FromResult(id == user.Id ? user : null);
            public Task AddAsync(User value) => Task.CompletedTask;
            public Task SaveChangesAsync() => Task.CompletedTask;
            public Task<bool> HasRoleAsync(Guid userId, Guid roleId) => Task.FromResult(false);
            public Task AssignRoleAsync(Guid userId, Guid roleId) => Task.CompletedTask;
            public Task RemoveRoleAsync(Guid userId, Guid roleId) => Task.CompletedTask;
            public Task UpdatePasswordHashAsync(Guid userId, string newPasswordHash)
            {
                user.PasswordHash = newPasswordHash;
                return Task.CompletedTask;
            }
            public Task UpdatePasswordAndClearFlagAsync(Guid userId, string newPasswordHash, bool clearMustChangePassword)
            {
                user.PasswordHash = newPasswordHash;
                if (clearMustChangePassword) user.MustChangePassword = false;
                return Task.CompletedTask;
            }
            public Task<List<User>> GetByOwnerAsync(Guid ownerUserId) => Task.FromResult(new List<User>());
        }

        public sealed class TokenRepository(PasswordResetToken token) : IPasswordResetTokenRepository
        {
            public Task AddAsync(PasswordResetToken value) => Task.CompletedTask;
            public Task<PasswordResetToken?> GetByRawTokenAsync(string rawToken) => Task.FromResult<PasswordResetToken?>(null);
            public Task<PasswordResetToken?> GetByTokenHashAsync(string tokenHash) =>
                Task.FromResult<PasswordResetToken?>(token.TokenHash == tokenHash ? token : null);
            public Task MarkUsedAsync(Guid id) { token.UsedAt = DateTime.UtcNow; return Task.CompletedTask; }
            public Task InvalidateAllForUserAsync(Guid userId) => Task.CompletedTask;
        }

        public sealed class RefreshTokenRepository : IRefreshTokenRepository
        {
            public int RevokeAllCount { get; private set; }
            public Task AddAsync(RefreshToken token) => Task.CompletedTask;
            public Task<RefreshToken?> GetByTokenAsync(string rawToken) => Task.FromResult<RefreshToken?>(null);
            public Task RevokeAsync(Guid id, string? replacedByTokenHash = null) => Task.CompletedTask;
            public Task RemoveExpiredAsync() => Task.CompletedTask;
            public Task RevokeAllForUserAsync(Guid userId) { RevokeAllCount++; return Task.CompletedTask; }
        }
    }
}
