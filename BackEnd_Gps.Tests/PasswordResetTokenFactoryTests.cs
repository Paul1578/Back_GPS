using fletflow.Infrastructure.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;

namespace BackEnd_Gps.Tests;

public sealed class PasswordResetTokenFactoryTests
{
    [Fact]
    public void Create_Uses256BitsOfRandomEntropyAndPersistsOnlyHash()
    {
        var factory = new PasswordResetTokenFactory();
        var (token, plainToken) = factory.Create(Guid.NewGuid(), 60);
        var base64 = plainToken.Replace('-', '+').Replace('_', '/');
        base64 = base64.PadRight((base64.Length + 3) / 4 * 4, '=');
        var decoded = Convert.FromBase64String(base64);

        Assert.Equal(32, decoded.Length);
        Assert.Equal(43, plainToken.Length);
        Assert.NotEqual(plainToken, token.TokenHash);
        Assert.Equal(64, token.TokenHash.Length);
    }

    [Fact]
    public void Create_UsesConfiguredExpirationMinutes()
    {
        var factory = new PasswordResetTokenFactory();
        var startedAt = DateTime.UtcNow;
        var (token, _) = factory.Create(Guid.NewGuid(), 37);
        var finishedAt = DateTime.UtcNow;

        Assert.InRange(token.ExpiresAt, startedAt.AddMinutes(37), finishedAt.AddMinutes(37));
    }

    [Fact]
    public void EmailSettings_DefaultsToOneHourAndAcceptsReasonableOverride()
    {
        var validator = new EmailSettingsValidator(new TestWebHostEnvironment("Development"));

        Assert.Equal(60, new EmailSettings().ResetTokenMinutes);
        Assert.True(validator.Validate(Options.DefaultName, new EmailSettings { ResetTokenMinutes = 30 }).Succeeded);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1441)]
    public void EmailSettings_RejectsInvalidOrUnreasonableExpiration(int expirationMinutes)
    {
        var validator = new EmailSettingsValidator(new TestWebHostEnvironment("Development"));

        Assert.False(validator.Validate(
            Options.DefaultName,
            new EmailSettings { ResetTokenMinutes = expirationMinutes }).Succeeded);
    }

    [Fact]
    public void EmailSettings_ProductionRequiresAbsoluteHttpsResetAndActivationUrls()
    {
        var validator = new EmailSettingsValidator(new TestWebHostEnvironment("Production"));
        var secureSettings = new EmailSettings
        {
            ResetPasswordUrl = "https://fleet.example/reset-password",
            ActivationUrl = "https://fleet.example/activate-account"
        };
        var insecureSettings = new EmailSettings
        {
            ResetPasswordUrl = "http://localhost:3000/reset-password",
            ActivationUrl = "/activate-account"
        };

        Assert.True(validator.Validate(Options.DefaultName, secureSettings).Succeeded);
        var result = validator.Validate(Options.DefaultName, insecureSettings);
        var failures = string.Join(" ", result.Failures ?? []);
        Assert.False(result.Succeeded);
        Assert.Contains("Email:ResetPasswordUrl", failures);
        Assert.Contains("Email:ActivationUrl", failures);
        Assert.DoesNotContain("localhost:3000", failures);
    }

    [Theory]
    [InlineData("http://localhost:3000/reset-password")]
    [InlineData("http://127.0.0.1:3000/reset-password")]
    [InlineData("https://fleet.example/reset-password")]
    public void EmailSettings_DevelopmentAllowsLoopbackHttpAndHttps(string resetPasswordUrl)
    {
        var validator = new EmailSettingsValidator(new TestWebHostEnvironment("Development"));
        var settings = new EmailSettings
        {
            ResetPasswordUrl = resetPasswordUrl,
            ActivationUrl = "http://[::1]:3000/activate-account"
        };

        Assert.True(validator.Validate(Options.DefaultName, settings).Succeeded);
    }

    [Fact]
    public void EmailSettings_DevelopmentRejectsHttpForNonLoopbackHost()
    {
        var validator = new EmailSettingsValidator(new TestWebHostEnvironment("Development"));
        var settings = new EmailSettings
        {
            ResetPasswordUrl = "http://fleet.example/reset-password"
        };

        Assert.False(validator.Validate(Options.DefaultName, settings).Succeeded);
    }

    private sealed class TestWebHostEnvironment(string environmentName) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "BackEnd_Gps.Tests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = string.Empty;
        public string EnvironmentName { get; set; } = environmentName;
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
