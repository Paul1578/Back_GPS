namespace fletflow.Infrastructure.Services;

public interface IEmailDeliveryQueue
{
    Task QueuePasswordResetAsync(Guid userId, string email, string resetLink, string plainToken);
}
