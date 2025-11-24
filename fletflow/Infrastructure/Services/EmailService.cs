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
        public string ResetPasswordUrl { get; set; } = "http://192.168.56.1:3000/reset-password";
        public string ActivationUrl { get; set; } = "http://192.168.56.1:3000/activate-account";
        public int ResetTokenMinutes { get; set; } = 1440; // 24 horas
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

        public async Task SendPasswordResetEmailAsync(string toEmail, string resetLink, string plainToken, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(_settings.SmtpHost) || _settings.SmtpHost.Contains("tu-dominio"))
            {
                throw new InvalidOperationException("SmtpHost no configurado. Actualiza appsettings.json -> Email.SmtpHost (ej: smtp.gmail.com).");
            }

            using var mail = new MailMessage
            {
                From = new MailAddress(_settings.From, _settings.DisplayName),
                Subject = "Restablecer contrasena",
                Body =
                    "Recibimos una solicitud para restablecer tu contrasena.\n\n" +
                    "Enlace directo (click): " + resetLink + "\n\n" +
                    "Enlace al front (abre y el token estara en la URL): " + _settings.ResetPasswordUrl + "\n\n" +
                    "Token (copiar/pegar en el front si lo necesitas): " + plainToken + "\n",
                IsBodyHtml = false
            };
            mail.To.Add(toEmail);

            using var client = BuildClient();

            try
            {
                await client.SendMailAsync(mail, cancellationToken);
                _logger.LogInformation(
                    "Correo de reset enviado a {Email} via {Host}:{Port} ssl={Ssl}",
                    toEmail, _settings.SmtpHost, _settings.SmtpPort, _settings.EnableSsl);
                Console.WriteLine($"[EmailService] Correo de reset enviado a {toEmail} via {_settings.SmtpHost}:{_settings.SmtpPort} ssl={_settings.EnableSsl}");
            }
            catch (Exception ex)
            {
                var smtpEx = ex as SmtpException;
                _logger.LogError(
                    ex,
                    "Error enviando correo de reset a {Email}. Host={Host} Port={Port} Ssl={Ssl} User={User} StatusCode={StatusCode} Inner={InnerMessage}",
                    toEmail,
                    _settings.SmtpHost,
                    _settings.SmtpPort,
                    _settings.EnableSsl,
                    _settings.User,
                    smtpEx?.StatusCode,
                    ex.InnerException?.Message);
                Console.WriteLine($"[EmailService] Error enviando correo a {toEmail}: {ex.GetType().Name} {ex.Message} StatusCode={smtpEx?.StatusCode} Inner={ex.InnerException?.Message}");
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
                Credentials = new NetworkCredential(_settings.User, _settings.Password),
                Timeout = 15000
            };
        }

        public async Task SendUserInvitationEmailAsync(string toEmail, string tempPassword, string activationLink, string activationToken, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(_settings.SmtpHost) || _settings.SmtpHost.Contains("tu-dominio"))
            {
                throw new InvalidOperationException("SmtpHost no configurado. Actualiza appsettings.json -> Email.SmtpHost (ej: smtp.gmail.com).");
            }

            using var mail = new MailMessage
            {
                From = new MailAddress(_settings.From, _settings.DisplayName),
                Subject = "Invitacion a la plataforma",
                Body =
                    "Has sido invitado a la plataforma.\n\n" +
                    "Correo: " + toEmail + "\n" +
                    "Password temporal: " + tempPassword + "\n\n" +
                    "Enlace para activar tu cuenta: " + activationLink + "\n\n" +
                    "Token (copiar/pegar si se requiere): " + activationToken + "\n\n" +
                    "Por seguridad, cambia tu contrasena al ingresar.",
                IsBodyHtml = false
            };
            mail.To.Add(toEmail);

            using var client = BuildClient();

            try
            {
                await client.SendMailAsync(mail, cancellationToken);
                _logger.LogInformation(
                    "Correo de invitacion enviado a {Email} via {Host}:{Port} ssl={Ssl}",
                    toEmail, _settings.SmtpHost, _settings.SmtpPort, _settings.EnableSsl);
            }
            catch (Exception ex)
            {
                var smtpEx = ex as SmtpException;
                _logger.LogError(
                    ex,
                    "Error enviando correo de invitacion a {Email}. Host={Host} Port={Port} Ssl={Ssl} User={User} StatusCode={StatusCode} Inner={InnerMessage}",
                    toEmail,
                    _settings.SmtpHost,
                    _settings.SmtpPort,
                    _settings.EnableSsl,
                    _settings.User,
                    smtpEx?.StatusCode,
                    ex.InnerException?.Message);
                throw;
            }
        }
    }
}
