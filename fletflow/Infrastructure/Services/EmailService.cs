using System;
using System.Net;
using System.Net.Mail;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace fletflow.Infrastructure.Services
{
    public class EmailSettings
    {
        public string From { get; set; } = default!;
        public string DisplayName { get; set; } = "FletFlow";
        public string SmtpHost { get; set; } = default!;
        public int SmtpPort { get; set; } = 587;
        public bool EnableSsl { get; set; } = true;
        public string User { get; set; } = default!;
        public string Password { get; set; } = default!;
        public string ResetPasswordUrl { get; set; } = "http://localhost:3000/reset-password";
        public string ActivationUrl { get; set; } = "http://localhost:3000/activate-account";
        public int ResetTokenMinutes { get; set; } = 60;
        public string FrontendBaseUrl { get; set; } = "http://192.168.56.1:3000";
    }
    public class EmailService : IEmailSender
        {
            private readonly EmailSettings _settings;
            private readonly ILogger<EmailService> _logger;

            public EmailService(IOptions<EmailSettings> options, ILogger<EmailService> logger)
            {
                _settings = options.Value;
                _logger = logger;
            }

            // =========================
            // TEMPLATE LOADER
            // =========================
            private static string LoadTemplate(string fileName)
            {
                var basePath = AppContext.BaseDirectory;
                var fullPath = Path.Combine(basePath, "EmailTemplates", fileName);

                if (!File.Exists(fullPath))
                    throw new FileNotFoundException($"Email template not found: {fullPath}");

                return File.ReadAllText(fullPath);
            }

            // =========================
            // PASSWORD RESET
            // =========================
            public async Task SendPasswordResetEmailAsync(
                string toEmail,
                string resetLink,
                string _,
                CancellationToken cancellationToken = default)
            {
                EnsureSmtpConfigured();

                var html = LoadTemplate("ResetPassword.html")
                    .Replace("{{RESET_LINK}}", resetLink)
                    .Replace("{{TOKEN_MINUTES}}", _settings.ResetTokenMinutes.ToString())
                    .Replace("{{APP_NAME}}", "FletFlow");

                using var mail = new MailMessage
                {
                    From = new MailAddress(_settings.From, _settings.DisplayName),
                    Subject = "Restablecer contraseña",
                    Body = html,
                    IsBodyHtml = true
                };

                mail.To.Add(toEmail);

                await SendAsync(mail, cancellationToken);
            }

            // =========================
            // USER INVITATION
            // =========================
            public async Task SendUserInvitationEmailAsync(
                string toEmail,
                string activationLink,
                CancellationToken cancellationToken = default)
            {
                EnsureSmtpConfigured();

                var html = LoadTemplate("Invitation.html")
                    .Replace("{{EMAIL}}", toEmail)
                    .Replace("{{ACTIVATION_LINK}}", activationLink)
                    .Replace("{{APP_NAME}}", "FletFlow");

                using var mail = new MailMessage
                {
                    From = new MailAddress(_settings.From, _settings.DisplayName),
                    Subject = "Invitación a FletFlow",
                    Body = html,
                    IsBodyHtml = true
                };

                mail.To.Add(toEmail);

                await SendAsync(mail, cancellationToken);
            }

            // =========================
            // SMTP
            // =========================
            private void EnsureSmtpConfigured()
            {
                if (string.IsNullOrWhiteSpace(_settings.SmtpHost) ||
                    _settings.SmtpHost.Contains("tu-dominio"))
                {
                    throw new InvalidOperationException(
                        "SmtpHost no configurado. Revisa appsettings.json -> Email");
                }
            }

            private async Task SendAsync(
                MailMessage mail,
                CancellationToken cancellationToken)
            {
                using var client = BuildClient();

                try
                {
                    await client.SendMailAsync(mail, cancellationToken);
                    _logger.LogInformation("Correo SMTP enviado correctamente.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        "Error enviando correo por SMTP. Tipo de fallo: {FailureType}.",
                        ex.GetType().Name);
                    throw;
                }
            }

            private SmtpClient BuildClient()
            {
                return new SmtpClient
                {
                    Host = _settings.SmtpHost,
                    Port = _settings.SmtpPort,
                    EnableSsl = _settings.EnableSsl,
                    DeliveryMethod = SmtpDeliveryMethod.Network,
                    UseDefaultCredentials = false,
                    Credentials = new NetworkCredential(
                        _settings.User,
                        _settings.Password),
                    Timeout = 15000
                };
            }
        }
    }
