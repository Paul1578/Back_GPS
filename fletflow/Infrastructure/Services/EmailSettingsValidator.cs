using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;

namespace fletflow.Infrastructure.Services;

public sealed class EmailSettingsValidator : IValidateOptions<EmailSettings>
{
    public const int MaximumResetTokenMinutes = 1440;
    private readonly IWebHostEnvironment _environment;

    public EmailSettingsValidator(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    public ValidateOptionsResult Validate(string? name, EmailSettings options)
    {
        var failures = new List<string>();
        if (options.ResetTokenMinutes is < 1 or > MaximumResetTokenMinutes)
        {
            failures.Add($"Email:ResetTokenMinutes debe estar entre 1 y {MaximumResetTokenMinutes} minutos.");
        }

        ValidateLink("Email:ResetPasswordUrl", options.ResetPasswordUrl, failures);
        ValidateLink("Email:ActivationUrl", options.ActivationUrl, failures);

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private void ValidateLink(string settingName, string? configuredUrl, ICollection<string> failures)
    {
        if (!Uri.TryCreate(configuredUrl, UriKind.Absolute, out var uri))
        {
            failures.Add($"{settingName} debe ser una URL absoluta HTTPS.");
            return;
        }

        var isHttps = string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
        var isDevelopmentLoopbackHttp = _environment.IsDevelopment()
            && string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            && uri.IsLoopback;

        if (!isHttps && !isDevelopmentLoopbackHttp)
            failures.Add($"{settingName} debe usar HTTPS; HTTP sólo se permite en Development con host loopback.");
    }
}
