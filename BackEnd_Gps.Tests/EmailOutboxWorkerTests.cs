using fletflow.Infrastructure.Persistence.Context;
using fletflow.Infrastructure.Persistence.Entities;
using fletflow.Infrastructure.Security.Hashing;
using fletflow.Infrastructure.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace BackEnd_Gps.Tests;

public sealed class EmailOutboxWorkerTests
{
    [Fact]
    public async Task Worker_DecryptsAndMarksOutboxMessageSent()
    {
        var result = await RunWorkerAsync(shouldFail: false, initialAttemptCount: 0);

        Assert.Equal("outbox-recipient@example.test", result.SenderCall!.Email);
        Assert.Equal("https://example.test/reset", result.SenderCall.Link);
        Assert.Equal("plain-reset-token", result.SenderCall.Token);
        Assert.NotNull(result.Message.SentAtUtc);
        Assert.Equal(0, result.Message.AttemptCount);
        Assert.Null(result.Message.LockToken);
        Assert.Null(result.Message.LockedUntilUtc);
    }

    [Fact]
    public async Task Worker_DeadLettersMessageAfterTwelfthFailure()
    {
        var result = await RunWorkerAsync(shouldFail: true, initialAttemptCount: 11);

        Assert.NotNull(result.SenderCall);
        Assert.Null(result.Message.SentAtUtc);
        Assert.Equal(12, result.Message.AttemptCount);
        Assert.NotNull(result.Message.DeadLetteredAtUtc);
        Assert.Equal(nameof(InvalidOperationException), result.Message.LastFailureType);
        Assert.Null(result.Message.LockToken);
        Assert.Null(result.Message.LockedUntilUtc);
    }

    [Fact]
    public async Task Worker_SuppressesEmailWhenResetTokenWasAlreadyInvalidated()
    {
        var result = await RunWorkerAsync(shouldFail: false, initialAttemptCount: 0,
            resetTokenIsActive: false, expectDelivery: false);

        Assert.Null(result.SenderCall);
        Assert.Null(result.Message.SentAtUtc);
        Assert.NotNull(result.Message.DeadLetteredAtUtc);
        Assert.Equal("ResetTokenInactive", result.Message.LastFailureType);
        Assert.Equal(0, result.Message.AttemptCount);
        Assert.Null(result.Message.LockToken);
        Assert.Null(result.Message.LockedUntilUtc);
    }

    private static async Task<WorkerResult> RunWorkerAsync(
        bool shouldFail,
        int initialAttemptCount,
        bool resetTokenIsActive = true,
        bool expectDelivery = true)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        var protectionProvider = new EphemeralDataProtectionProvider();
        var userId = Guid.NewGuid();
        const string rawToken = "plain-reset-token";
        Guid messageId;

        await using (var db = new AppDbContext(options))
        {
            await db.Database.EnsureCreatedAsync();
            db.Users.Add(new UserEntity
            {
                Id = userId,
                Username = "outbox-test",
                Email = "outbox-recipient@example.test",
                PasswordHash = "not-a-real-password-hash",
                CreatedAt = DateTime.UtcNow
            });
            db.PasswordResetTokens.Add(new PasswordResetTokenEntity
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                TokenHash = TokenHashing.Sha256(rawToken),
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddMinutes(10),
                UsedAt = resetTokenIsActive ? null : DateTime.UtcNow
            });
            var queue = new EmailDeliveryQueue(db, protectionProvider);
            await queue.QueuePasswordResetAsync(userId, "outbox-recipient@example.test",
                "https://example.test/reset", rawToken);
            await db.SaveChangesAsync();
            var message = await db.EmailOutbox.SingleAsync();
            message.AttemptCount = initialAttemptCount;
            await db.SaveChangesAsync();
            messageId = message.Id;
        }

        var sender = new RecordingEmailSender(shouldFail);
        using var services = new ServiceCollection()
            .AddLogging()
            .AddScoped(_ => new AppDbContext(options))
            .AddSingleton<IEmailSender>(sender)
            .BuildServiceProvider();
        var worker = new EmailOutboxWorker(
            services.GetRequiredService<IServiceScopeFactory>(),
            protectionProvider,
            NullLogger<EmailOutboxWorker>.Instance);

        try
        {
            await worker.StartAsync(CancellationToken.None);
            if (expectDelivery)
            {
                var completed = await Task.WhenAny(sender.Called.Task, Task.Delay(TimeSpan.FromSeconds(5)));
                Assert.Same(sender.Called.Task, completed);
            }

            EmailOutboxEntity? persisted = null;
            var deadline = DateTime.UtcNow.AddSeconds(2);
            while (DateTime.UtcNow < deadline)
            {
                await using var verify = new AppDbContext(options);
                persisted = await verify.EmailOutbox.AsNoTracking().SingleOrDefaultAsync(item => item.Id == messageId);
                if (persisted?.SentAtUtc is not null || persisted?.DeadLetteredAtUtc is not null)
                    break;
                await Task.Delay(20);
            }

            Assert.NotNull(persisted);
            Assert.True(persisted!.SentAtUtc.HasValue || persisted.DeadLetteredAtUtc.HasValue);
            Assert.Equal(expectDelivery, sender.Called.Task.IsCompletedSuccessfully);
            return new WorkerResult(persisted, sender.Called.Task.IsCompletedSuccessfully ? sender.Called.Task.Result : null);
        }
        finally
        {
            await worker.StopAsync(CancellationToken.None);
        }
    }

    private sealed record SenderCall(string Email, string Link, string Token);
    private sealed record WorkerResult(EmailOutboxEntity Message, SenderCall? SenderCall);

    private sealed class RecordingEmailSender(bool shouldFail) : IEmailSender
    {
        public TaskCompletionSource<SenderCall> Called { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task SendPasswordResetEmailAsync(string toEmail, string link, string plainToken,
            CancellationToken cancellationToken = default)
        {
            Called.TrySetResult(new SenderCall(toEmail, link, plainToken));
            return shouldFail ? Task.FromException(new InvalidOperationException("SMTP unavailable")) : Task.CompletedTask;
        }

        public Task SendUserInvitationEmailAsync(string toEmail, string activationLink,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
