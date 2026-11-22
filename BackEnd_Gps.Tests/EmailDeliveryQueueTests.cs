using fletflow.Infrastructure.Persistence.Context;
using fletflow.Infrastructure.Persistence.Entities;
using fletflow.Infrastructure.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace BackEnd_Gps.Tests;

public sealed class EmailDeliveryQueueTests
{
    [Fact]
    public async Task QueuePasswordReset_TracksOnlyProtectedPayloadInOutbox()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new AppDbContext(options);
        var queue = new EmailDeliveryQueue(db, new EphemeralDataProtectionProvider());
        const string email = "private-recipient@example.test";
        const string link = "https://example.test/reset?token=private-raw-token";
        const string token = "private-raw-token";

        await queue.QueuePasswordResetAsync(Guid.NewGuid(), email, link, token);

        var row = Assert.Single(db.EmailOutbox.Local);
        Assert.DoesNotContain(email, row.ProtectedPayload, StringComparison.Ordinal);
        Assert.DoesNotContain(link, row.ProtectedPayload, StringComparison.Ordinal);
        Assert.DoesNotContain(token, row.ProtectedPayload, StringComparison.Ordinal);
        Assert.Equal(0, row.AttemptCount);
        Assert.Null(row.SentAtUtc);
    }
}
