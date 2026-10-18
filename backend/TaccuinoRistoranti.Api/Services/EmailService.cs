using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace TaccuinoRistoranti.Api.Services;

public interface IEmailService
{
    Task SendPasswordResetEmailAsync(string toEmail, string userName, string resetToken);
}

public sealed class EmailOptions
{
    public const string SectionName = "Email";

    public string SmtpHost { get; init; } = string.Empty;
    public int SmtpPort { get; init; } = 587;
    public bool EnableSsl { get; init; } = true;
    public string Username { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public string FromEmail { get; init; } = string.Empty;
    public string FromName { get; init; } = "Taccuino Ristoranti";
    public string FrontendBaseUrl { get; init; } = "http://localhost:5173";
}

public class EmailService(IOptions<EmailOptions> options, ILogger<EmailService> logger) : IEmailService
{
    private readonly EmailOptions _options = options.Value;

    public async Task SendPasswordResetEmailAsync(string toEmail, string userName, string resetToken)
    {
        if (string.IsNullOrWhiteSpace(_options.SmtpHost)
            || string.IsNullOrWhiteSpace(_options.Username)
            || string.IsNullOrWhiteSpace(_options.Password)
            || string.IsNullOrWhiteSpace(_options.FromEmail))
        {
            throw new InvalidOperationException("Configurazione SMTP mancante. Imposta la sezione Email in appsettings o user-secrets.");
        }

        var encodedToken = Uri.EscapeDataString(resetToken);
        var resetUrl = $"{_options.FrontendBaseUrl.TrimEnd('/')}/reset-password?token={encodedToken}";

        var subject = "Reset Password - Taccuino Ristoranti";
        var body = $"""
Ciao {userName},

Abbiamo ricevuto una richiesta di reset password per il tuo account.

Per impostare una nuova password, visita questo link:
{resetUrl}

IMPORTANTE: Il link scade tra 1 ora.

Se non hai richiesto tu il reset, puoi ignorare questa email.

Team Taccuino Ristoranti
""";

        using var client = new SmtpClient(_options.SmtpHost, _options.SmtpPort)
        {
            EnableSsl = _options.EnableSsl,
            UseDefaultCredentials = false,
            Credentials = new NetworkCredential(_options.Username, _options.Password)
        };

        using var message = new MailMessage
        {
            From = new MailAddress(_options.FromEmail, _options.FromName),
            Subject = subject,
            Body = body,
            IsBodyHtml = false
        };

        message.To.Add(toEmail);

        await client.SendMailAsync(message);
        logger.LogInformation("Password reset email inviata a {Email}", toEmail);
    }
}
