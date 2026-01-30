using Chivato.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace Chivato.Infrastructure.Services;

public class EmailServiceOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public string FromEmail { get; set; } = "noreply@chivato.io";
    public string FromName { get; set; } = "Chivato";
}

public class EmailService : IEmailService
{
    private readonly SendGridClient _client;
    private readonly EmailServiceOptions _options;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IOptions<EmailServiceOptions> options, ILogger<EmailService> logger)
    {
        _options = options.Value;
        _client = new SendGridClient(_options.ApiKey);
        _logger = logger;
    }

    public async Task SendAsync(
        string to,
        string subject,
        string htmlBody,
        CancellationToken ct = default)
    {
        try
        {
            var msg = new SendGridMessage
            {
                From = new EmailAddress(_options.FromEmail, _options.FromName),
                Subject = subject,
                HtmlContent = htmlBody
            };

            msg.AddTo(new EmailAddress(to));

            var response = await _client.SendEmailAsync(msg, ct);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Body.ReadAsStringAsync(ct);
                _logger.LogWarning("Failed to send email to {To}: {StatusCode} - {Body}",
                    to, response.StatusCode, body);
            }
            else
            {
                _logger.LogInformation("Email sent to {To}: {Subject}", to, subject);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending email to {To}", to);
            throw;
        }
    }

    public async Task SendBulkAsync(
        IEnumerable<string> recipients,
        string subject,
        string htmlBody,
        CancellationToken ct = default)
    {
        try
        {
            var msg = new SendGridMessage
            {
                From = new EmailAddress(_options.FromEmail, _options.FromName),
                Subject = subject,
                HtmlContent = htmlBody
            };

            foreach (var recipient in recipients)
            {
                msg.AddTo(new EmailAddress(recipient));
            }

            var response = await _client.SendEmailAsync(msg, ct);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Body.ReadAsStringAsync(ct);
                _logger.LogWarning("Failed to send bulk email: {StatusCode} - {Body}",
                    response.StatusCode, body);
            }
            else
            {
                _logger.LogInformation("Bulk email sent: {Subject}", subject);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending bulk email");
            throw;
        }
    }
}
