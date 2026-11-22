namespace fletflow.Infrastructure.Services;

public interface IEmailDeliveryQueue
{
    bool TryQueuePasswordReset(string email, string resetLink, string plainToken);
}
