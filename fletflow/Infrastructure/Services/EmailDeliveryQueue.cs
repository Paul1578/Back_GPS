using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace fletflow.Infrastructure.Services;

public sealed class EmailDeliveryQueue : BackgroundService, IEmailDeliveryQueue
{
    private readonly Channel<PasswordResetEmail> _queue = Channel.CreateBounded<PasswordResetEmail>(
        new BoundedChannelOptions(256)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        });
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EmailDeliveryQueue> _logger;

    public EmailDeliveryQueue(IServiceScopeFactory scopeFactory, ILogger<EmailDeliveryQueue> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public bool TryQueuePasswordReset(string email, string resetLink, string plainToken) =>
        _queue.Writer.TryWrite(new PasswordResetEmail(email, resetLink, plainToken));

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var message in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var sender = scope.ServiceProvider.GetRequiredService<IEmailSender>();
                await sender.SendPasswordResetEmailAsync(
                    message.Email,
                    message.ResetLink,
                    message.PlainToken,
                    stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Password reset email delivery failed.");
            }
        }
    }

    private sealed record PasswordResetEmail(string Email, string ResetLink, string PlainToken);
}
