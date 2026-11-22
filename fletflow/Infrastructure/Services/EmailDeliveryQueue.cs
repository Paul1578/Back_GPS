using System.Text.Json;
using fletflow.Infrastructure.Persistence.Context;
using fletflow.Infrastructure.Persistence.Entities;
using Microsoft.AspNetCore.DataProtection;

namespace fletflow.Infrastructure.Services;

/// <summary>Adds a protected message to the current request's DbContext/unit of work.</summary>
public sealed class EmailDeliveryQueue : IEmailDeliveryQueue
{
    private readonly AppDbContext _db;
    private readonly IDataProtector _protector;

    public EmailDeliveryQueue(AppDbContext db, IDataProtectionProvider protectionProvider)
    {
        _db = db;
        _protector = protectionProvider.CreateProtector("FletFlow.EmailOutbox.PasswordReset.v1");
    }

    public Task QueuePasswordResetAsync(Guid userId, string email, string resetLink, string plainToken)
    {
        var serialized = JsonSerializer.Serialize(new PasswordResetEmailPayload(email, resetLink, plainToken));
        _db.Set<EmailOutboxEntity>().Add(new EmailOutboxEntity
        {
            UserId = userId,
            ProtectedPayload = _protector.Protect(serialized),
            CreatedAtUtc = DateTime.UtcNow,
            NextAttemptAtUtc = DateTime.UtcNow
        });
        return Task.CompletedTask;
    }
}
