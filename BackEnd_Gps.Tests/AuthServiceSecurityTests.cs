using fletflow.Application.DTOs.Auth;
using fletflow.Domain.Auth.Entities;
using fletflow.Domain.Auth.Repositories;
using fletflow.Domain.Fleet.Repositories;
using fletflow.Infrastructure.Persistence.Contracts;
using fletflow.Infrastructure.Services;
using System.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace BackEnd_Gps.Tests;

public sealed class AuthServiceSecurityTests
{
    [Fact]
    public async Task LogoutAll_InvalidatesAccessTokenVersionAndRevokesRefreshTokens()
    {
        var user = new User { Email = "member@example.test", TokenVersion = 4 };
        var unitOfWork = new RecordingUnitOfWork(user);
        var service = CreateService(unitOfWork, new RecordingEmailQueue());

        await service.LogoutAllAsync(user.Id);

        Assert.Equal(5, user.TokenVersion);
        Assert.Equal(1, unitOfWork.LockUserSessionCount);
        Assert.Equal(1, unitOfWork.RefreshTokens.RevokeAllCount);
        Assert.Equal(1, unitOfWork.CommitCount);
    }

    [Fact]
    public async Task ForgotPassword_HidesAccountExistenceAndQueuesDeliveryOutOfBand()
    {
        var user = new User { Email = "known@example.test" };
        var unitOfWork = new RecordingUnitOfWork(user);
        var emailQueue = new RecordingEmailQueue();
        var service = CreateService(unitOfWork, emailQueue);

        var knownStarted = Stopwatch.GetTimestamp();
        var known = await service.ForgotPasswordAsync("known@example.test");
        var knownElapsed = Stopwatch.GetElapsedTime(knownStarted);
        var unknownStarted = Stopwatch.GetTimestamp();
        var unknown = await service.ForgotPasswordAsync("unknown@example.test");
        var unknownElapsed = Stopwatch.GetElapsedTime(unknownStarted);

        Assert.Equal(known.Message, unknown.Message);
        Assert.Null(known.ExpiresAt);
        Assert.Null(unknown.ExpiresAt);
        Assert.Null(known.DebugToken);
        Assert.Null(unknown.DebugToken);
        Assert.Single(unitOfWork.PasswordResetTokens.Added);
        Assert.Equal(1, unitOfWork.CommitCount);
        Assert.Equal(1, emailQueue.QueuedCount);
        Assert.True(knownElapsed >= TimeSpan.FromMilliseconds(200));
        Assert.True(unknownElapsed >= TimeSpan.FromMilliseconds(200));
    }

    private static AuthService CreateService(IUnitOfWork unitOfWork, IEmailDeliveryQueue emailDeliveryQueue) => new(
        unitOfWork,
        null!,
        emailDeliveryQueue,
        new PasswordResetTokenFactory(),
        Options.Create(new EmailSettings { ResetPasswordUrl = "https://example.test/reset" }),
        NullLogger<AuthService>.Instance);

    private sealed class RecordingUnitOfWork : IUnitOfWork
    {
        public RecordingRefreshTokens RefreshTokens { get; } = new();
        public RecordingPasswordResetTokens PasswordResetTokens { get; } = new();
        public int CommitCount { get; private set; }
        public int LockUserSessionCount { get; private set; }
        public IUserRepository Users { get; }
        IRoleRepository IUnitOfWork.Roles => null!;
        IRefreshTokenRepository IUnitOfWork.RefreshTokens => RefreshTokens;
        IPasswordResetTokenRepository IUnitOfWork.PasswordResetTokens => PasswordResetTokens;
        IVehicleRepository IUnitOfWork.Vehicles => null!;
        IDriverRepository IUnitOfWork.Drivers => null!;
        IRouteRepository IUnitOfWork.Routes => null!;
        IRoutePositionRepository IUnitOfWork.RoutePositions => null!;

        public RecordingUnitOfWork(User user)
        {
            Users = new RecordingUsers(user);
        }

        public Task<int> CommitAsync() { CommitCount++; return Task.FromResult(1); }
        public Task LockUserSessionAsync(Guid userId) { LockUserSessionCount++; return Task.CompletedTask; }
        public void Rollback() { }

        private sealed class RecordingUsers(User user) : IUserRepository
        {
            public Task<User?> GetByEmailAsync(string email) => Task.FromResult<User?>(
                string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase) ? user : null);
            public Task<User?> GetByIdAsync(Guid id) => Task.FromResult<User?>(id == user.Id ? user : null);
            public Task AddAsync(User value) => Task.CompletedTask;
            public Task SaveChangesAsync() => Task.CompletedTask;
            public Task<bool> HasRoleAsync(Guid userId, Guid roleId) => Task.FromResult(false);
            public Task AssignRoleAsync(Guid userId, Guid roleId) => Task.CompletedTask;
            public Task RemoveRoleAsync(Guid userId, Guid roleId) => Task.CompletedTask;
            public Task UpdatePasswordHashAsync(Guid userId, string newPasswordHash) => Task.CompletedTask;
            public Task UpdatePasswordAndClearFlagAsync(Guid userId, string newPasswordHash, bool clearMustChangePassword) => Task.CompletedTask;
            public Task IncrementTokenVersionAsync(Guid userId) { user.TokenVersion++; return Task.CompletedTask; }
            public Task<List<User>> GetByOwnerAsync(Guid ownerUserId) => Task.FromResult(new List<User>());
        }

        public sealed class RecordingRefreshTokens : IRefreshTokenRepository
        {
            public int RevokeAllCount { get; private set; }
            public Task AddAsync(RefreshToken token) => Task.CompletedTask;
            public Task<RefreshToken?> GetByTokenAsync(string rawToken) => Task.FromResult<RefreshToken?>(null);
            public Task RevokeAsync(Guid id, string? replacedByTokenHash = null) => Task.CompletedTask;
        public Task<bool> TryRotateAsync(Guid currentTokenId, RefreshToken replacement) => Task.FromResult(false);
            public Task RemoveExpiredAsync() => Task.CompletedTask;
            public Task RevokeAllForUserAsync(Guid userId) { RevokeAllCount++; return Task.CompletedTask; }
        }

        public sealed class RecordingPasswordResetTokens : IPasswordResetTokenRepository
        {
            public List<PasswordResetToken> Added { get; } = [];
            public Task AddAsync(PasswordResetToken token) { Added.Add(token); return Task.CompletedTask; }
            public Task<PasswordResetToken?> GetByRawTokenAsync(string rawToken) => Task.FromResult<PasswordResetToken?>(null);
            public Task<PasswordResetToken?> GetByTokenHashAsync(string tokenHash) => Task.FromResult<PasswordResetToken?>(null);
            public Task MarkUsedAsync(Guid id) => Task.CompletedTask;
            public Task InvalidateAllForUserAsync(Guid userId) => Task.CompletedTask;
        }
    }

    private sealed class RecordingEmailQueue : IEmailDeliveryQueue
    {
        public int QueuedCount { get; private set; }
        public bool TryQueuePasswordReset(string email, string resetLink, string plainToken)
        {
            QueuedCount++;
            return false; // Queue unavailability must not change the public response.
        }
    }
}
