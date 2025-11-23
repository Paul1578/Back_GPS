using System.Threading;
using System.Threading.Tasks;

namespace fletflow.Infrastructure.Services
{
    public interface IEmailSender
    {
        Task SendPasswordResetEmailAsync(string toEmail, string link, string plainToken, CancellationToken cancellationToken = default);
    }
}
