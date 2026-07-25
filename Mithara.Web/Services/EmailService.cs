using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace Mithara.Web.Services;

public class SmtpOptions
{
    public string Host { get; set; } = "";
    public int Port { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string FromEmail { get; set; } = "";
    public string FromName { get; set; } = "Mithara Online";
}

public class EmailService
{
    private readonly SmtpOptions _options;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IOptions<SmtpOptions> options, ILogger<EmailService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendPasswordResetAsync(string toEmail, string username, string resetLink)
    {
        if (string.IsNullOrWhiteSpace(_options.Host) || string.IsNullOrWhiteSpace(_options.FromEmail))
        {
            _logger.LogWarning("SMTP nao configurado. Link de recuperacao para {Email}: {ResetLink}", toEmail, resetLink);
            return;
        }

        using var message = new MailMessage
        {
            From = new MailAddress(_options.FromEmail, _options.FromName),
            Subject = "Recuperacao de senha - Mithara Online",
            Body = $"""
                Ola, {username}.

                Recebemos uma solicitacao para recuperar sua senha do Mithara Online.
                Acesse o link abaixo para criar uma nova senha. Ele expira em 30 minutos e so pode ser usado uma vez.

                {resetLink}

                Se voce nao pediu essa recuperacao, ignore este e-mail.
                """,
        };
        message.To.Add(toEmail);

        using var client = new SmtpClient(_options.Host, _options.Port)
        {
            EnableSsl = _options.EnableSsl,
        };

        if (!string.IsNullOrWhiteSpace(_options.Username))
            client.Credentials = new NetworkCredential(_options.Username, _options.Password);

        await client.SendMailAsync(message);
    }
}
