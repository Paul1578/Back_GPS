using System.Text.Json;
using fletflow.Infrastructure.Persistence.Context;
using fletflow.Infrastructure.Persistence.Entities;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace fletflow.Infrastructure.Services;

public sealed class EmailOutboxWorker : BackgroundService
{
    private const int MaxAttempts = 12;
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan ClaimLease = TimeSpan.FromMinutes(2);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IDataProtectionProvider _protectionProvider;
    private readonly ILogger<EmailOutboxWorker> _logger;

    public EmailOutboxWorker(
        IServiceScopeFactory scopeFactory,
        IDataProtectionProvider protectionProvider,
        ILogger<EmailOutboxWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _protectionProvider = protectionProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var lastPurgeAtUtc = DateTime.UtcNow;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (DateTime.UtcNow - lastPurgeAtUtc >= TimeSpan.FromHours(6))
                {
                    await PurgeOldMessagesAsync(stoppingToken);
                    lastPurgeAtUtc = DateTime.UtcNow;
                }
                if (!await DeliverOneAsync(stoppingToken))
                    await Task.Delay(PollInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError("Email outbox polling failed ({FailureType}).", exception.GetType().Name);
                await Task.Delay(PollInterval, stoppingToken);
            }
        }
    }

    private async Task PurgeOldMessagesAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var sentBefore = DateTime.UtcNow.AddDays(-30);
        var deadBefore = DateTime.UtcNow.AddDays(-90);
        await db.Set<EmailOutboxEntity>()
            .Where(message => (message.SentAtUtc != null && message.SentAtUtc < sentBefore)
                || (message.DeadLetteredAtUtc != null && message.DeadLetteredAtUtc < deadBefore))
            .ExecuteDeleteAsync(cancellationToken);
    }

    private async Task<bool> DeliverOneAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = DateTime.UtcNow;
        var candidateIds = await db.Set<EmailOutboxEntity>()
            .AsNoTracking()
            .Where(message => message.SentAtUtc == null && message.DeadLetteredAtUtc == null
                && message.NextAttemptAtUtc <= now
                && (message.LockedUntilUtc == null || message.LockedUntilUtc < now))
            .OrderBy(message => message.CreatedAtUtc)
            .Select(message => message.Id)
            .Take(10)
            .ToListAsync(cancellationToken);

        foreach (var id in candidateIds)
        {
            var leaseUntil = now.Add(ClaimLease);
            var lockToken = Guid.NewGuid();
            var claimed = await db.Set<EmailOutboxEntity>()
                .Where(message => message.Id == id && message.SentAtUtc == null
                    && message.DeadLetteredAtUtc == null && message.NextAttemptAtUtc <= now
                    && (message.LockedUntilUtc == null || message.LockedUntilUtc < now))
                .ExecuteUpdateAsync(update => update
                    .SetProperty(message => message.LockedUntilUtc, leaseUntil)
                    .SetProperty(message => message.LockToken, lockToken), cancellationToken);
            if (claimed == 0)
                continue;

            var message = await db.Set<EmailOutboxEntity>().SingleAsync(item => item.Id == id, cancellationToken);
            await DeliverClaimedAsync(db, message, lockToken, cancellationToken);
            return true;
        }

        return false;
    }

    private async Task DeliverClaimedAsync(AppDbContext db, EmailOutboxEntity message, Guid lockToken, CancellationToken cancellationToken)
    {
        try
        {
            var protector = _protectionProvider.CreateProtector("FletFlow.EmailOutbox.PasswordReset.v1");
            var json = protector.Unprotect(message.ProtectedPayload);
            var payload = JsonSerializer.Deserialize<PasswordResetEmailPayload>(json)
                ?? throw new InvalidOperationException("El contenido cifrado del outbox no es válido.");
            using (var sender = _scopeFactory.CreateScope())
            {
                var emailSender = sender.ServiceProvider.GetRequiredService<IEmailSender>();
                await emailSender.SendPasswordResetEmailAsync(payload.Email, payload.ResetLink, payload.PlainToken, cancellationToken);
            }

            await db.Set<EmailOutboxEntity>()
                .Where(item => item.Id == message.Id && item.LockToken == lockToken)
                .ExecuteUpdateAsync(update => update
                    .SetProperty(item => item.SentAtUtc, DateTime.UtcNow)
                    .SetProperty(item => item.LockedUntilUtc, (DateTime?)null)
                    .SetProperty(item => item.LockToken, (Guid?)null)
                    .SetProperty(item => item.LastFailureType, (string?)null), cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The lease expires and another worker retries after restart.
            throw;
        }
        catch (Exception exception)
        {
            var attempts = message.AttemptCount + 1;
            var failureType = exception.GetType().Name[..Math.Min(100, exception.GetType().Name.Length)];
            var deadLetteredAt = attempts >= MaxAttempts ? DateTime.UtcNow : (DateTime?)null;
            var nextAttempt = attempts >= MaxAttempts
                ? message.NextAttemptAtUtc
                : DateTime.UtcNow.AddMinutes(Math.Min(360, Math.Pow(2, attempts)));
            var affected = await db.Set<EmailOutboxEntity>()
                .Where(item => item.Id == message.Id && item.LockToken == lockToken)
                .ExecuteUpdateAsync(update => update
                    .SetProperty(item => item.AttemptCount, attempts)
                    .SetProperty(item => item.LastFailureType, failureType)
                    .SetProperty(item => item.LockedUntilUtc, (DateTime?)null)
                    .SetProperty(item => item.LockToken, (Guid?)null)
                    .SetProperty(item => item.DeadLetteredAtUtc, deadLetteredAt)
                    .SetProperty(item => item.NextAttemptAtUtc, nextAttempt), cancellationToken);
            if (affected == 0)
                return; // This worker's lease was replaced; it must not overwrite the current claim.

            if (deadLetteredAt.HasValue)
            {
                _logger.LogError("Password reset email moved to dead letter after {Attempts} attempts ({FailureType}).", attempts, failureType);
            }
            else
            {
                _logger.LogWarning("Password reset email delivery failed; retry scheduled ({FailureType}, attempt {Attempt}).", failureType, attempts);
            }
        }
    }
}
