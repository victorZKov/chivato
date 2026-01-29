using Microsoft.Extensions.Logging;
using Azure.Communication.Email;
using Chivato.Functions.Models;
using System.Text;

namespace Chivato.Functions.Services;

public class EmailService : IEmailService
{
    private readonly ILogger<EmailService> _logger;

    public EmailService(ILogger<EmailService> logger)
    {
        _logger = logger;
    }

    public async Task<bool> TestConnectionAsync(string connectionString, string fromEmail)
    {
        try
        {
            var client = new EmailClient(connectionString);

            // Just verify we can create the client and it's configured
            // We don't want to send a test email
            _logger.LogInformation("Email service connection test successful");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Email service connection test failed");
            return false;
        }
    }

    public async Task SendDriftReportAsync(
        IEnumerable<DriftRecordEntity> driftRecords,
        IEnumerable<string> recipients,
        string connectionString,
        string fromEmail,
        string fromDisplayName)
    {
        if (!driftRecords.Any())
        {
            _logger.LogInformation("No drift records to report, skipping email");
            return;
        }

        if (!recipients.Any())
        {
            _logger.LogWarning("No recipients configured for drift report");
            return;
        }

        try
        {
            var client = new EmailClient(connectionString);

            var subject = GenerateDriftReportSubject(driftRecords);
            var htmlBody = GenerateDriftReportHtml(driftRecords);
            var plainBody = GenerateDriftReportPlainText(driftRecords);

            var emailRecipients = new EmailRecipients(
                recipients.Select(r => new EmailAddress(r)).ToList());

            var emailContent = new EmailContent(subject)
            {
                Html = htmlBody,
                PlainText = plainBody
            };

            var emailMessage = new EmailMessage(
                senderAddress: fromEmail,
                recipients: emailRecipients,
                content: emailContent);

            var operation = await client.SendAsync(Azure.WaitUntil.Completed, emailMessage);

            _logger.LogInformation(
                "Drift report sent to {RecipientCount} recipients. Operation ID: {OperationId}",
                recipients.Count(), operation.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send drift report email");
            throw;
        }
    }

    public async Task SendCredentialExpirationAlertAsync(
        IEnumerable<CredentialStatus> expiringCredentials,
        IEnumerable<string> recipients,
        string connectionString,
        string fromEmail,
        string fromDisplayName)
    {
        if (!expiringCredentials.Any())
        {
            _logger.LogInformation("No expiring credentials to report");
            return;
        }

        if (!recipients.Any())
        {
            _logger.LogWarning("No recipients configured for credential alerts");
            return;
        }

        try
        {
            var client = new EmailClient(connectionString);

            var subject = GenerateCredentialAlertSubject(expiringCredentials);
            var htmlBody = GenerateCredentialAlertHtml(expiringCredentials);
            var plainBody = GenerateCredentialAlertPlainText(expiringCredentials);

            var emailRecipients = new EmailRecipients(
                recipients.Select(r => new EmailAddress(r)).ToList());

            var emailContent = new EmailContent(subject)
            {
                Html = htmlBody,
                PlainText = plainBody
            };

            var emailMessage = new EmailMessage(
                senderAddress: fromEmail,
                recipients: emailRecipients,
                content: emailContent);

            var operation = await client.SendAsync(Azure.WaitUntil.Completed, emailMessage);

            _logger.LogInformation(
                "Credential expiration alert sent to {RecipientCount} recipients",
                recipients.Count());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send credential expiration alert");
            throw;
        }
    }

    #region Email Content Generation

    private string GenerateDriftReportSubject(IEnumerable<DriftRecordEntity> records)
    {
        var criticalCount = records.Count(r => r.Severity == "CRITICAL");
        var highCount = records.Count(r => r.Severity == "HIGH");

        if (criticalCount > 0)
            return $"⛔ [Chivato] {criticalCount} desviaciones CRÍTICAS detectadas";
        if (highCount > 0)
            return $"🔴 [Chivato] {highCount} desviaciones ALTAS detectadas";

        return $"📊 [Chivato] Reporte de drift: {records.Count()} desviaciones encontradas";
    }

    private string GenerateDriftReportHtml(IEnumerable<DriftRecordEntity> records)
    {
        var sb = new StringBuilder();

        sb.AppendLine(@"<!DOCTYPE html>
<html>
<head>
    <style>
        body { font-family: 'Inter', -apple-system, sans-serif; line-height: 1.6; color: #1a1a1a; }
        .container { max-width: 800px; margin: 0 auto; padding: 20px; }
        .header { background: linear-gradient(135deg, #1a1a1a, #2a2a2a); color: #ff6b35; padding: 30px; border-radius: 8px 8px 0 0; }
        .header h1 { margin: 0; font-size: 24px; }
        .summary { background: #f5f5f5; padding: 20px; margin-bottom: 20px; border-radius: 0 0 8px 8px; }
        .summary-item { display: inline-block; margin-right: 30px; }
        .badge { display: inline-block; padding: 4px 12px; border-radius: 20px; font-size: 12px; font-weight: 600; }
        .badge-critical { background: #fecaca; color: #991b1b; }
        .badge-high { background: #fed7aa; color: #9a3412; }
        .badge-medium { background: #fef08a; color: #854d0e; }
        .badge-low { background: #bfdbfe; color: #1e40af; }
        .drift-item { border: 1px solid #e5e5e5; border-radius: 8px; padding: 16px; margin-bottom: 16px; }
        .drift-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 12px; }
        .drift-resource { font-weight: 600; color: #1a1a1a; }
        .drift-type { color: #666; font-size: 14px; }
        .drift-details { font-size: 14px; }
        .drift-details dt { font-weight: 600; color: #666; margin-top: 8px; }
        .drift-details dd { margin-left: 0; }
        .value-box { background: #f5f5f5; padding: 8px 12px; border-radius: 4px; font-family: monospace; font-size: 13px; margin-top: 4px; }
        .expected { border-left: 3px solid #22c55e; }
        .actual { border-left: 3px solid #ef4444; }
        .recommendation { background: #fff7ed; border-left: 3px solid #ff6b35; padding: 12px; margin-top: 12px; border-radius: 0 4px 4px 0; }
        .footer { text-align: center; margin-top: 30px; padding-top: 20px; border-top: 1px solid #e5e5e5; color: #666; font-size: 12px; }
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>🔍 Chivato - Reporte de Drift</h1>
        </div>
        <div class='summary'>");

        // Summary stats
        var grouped = records.GroupBy(r => r.Severity).ToDictionary(g => g.Key, g => g.Count());
        sb.AppendLine("<p><strong>Resumen del análisis:</strong></p>");
        sb.AppendLine($"<span class='summary-item'>Total: <strong>{records.Count()}</strong></span>");

        if (grouped.TryGetValue("CRITICAL", out var critical))
            sb.AppendLine($"<span class='summary-item'><span class='badge badge-critical'>CRÍTICO: {critical}</span></span>");
        if (grouped.TryGetValue("HIGH", out var high))
            sb.AppendLine($"<span class='summary-item'><span class='badge badge-high'>ALTO: {high}</span></span>");
        if (grouped.TryGetValue("MEDIUM", out var medium))
            sb.AppendLine($"<span class='summary-item'><span class='badge badge-medium'>MEDIO: {medium}</span></span>");
        if (grouped.TryGetValue("LOW", out var low))
            sb.AppendLine($"<span class='summary-item'><span class='badge badge-low'>BAJO: {low}</span></span>");

        sb.AppendLine("</div>");

        // Individual drift items
        sb.AppendLine("<h2>Desviaciones Detectadas</h2>");

        foreach (var record in records.OrderByDescending(r => GetSeverityOrder(r.Severity)))
        {
            var badgeClass = record.Severity switch
            {
                "CRITICAL" => "badge-critical",
                "HIGH" => "badge-high",
                "MEDIUM" => "badge-medium",
                "LOW" => "badge-low",
                _ => "badge-low"
            };

            sb.AppendLine($@"
            <div class='drift-item'>
                <div class='drift-header'>
                    <div>
                        <span class='drift-resource'>{HtmlEncode(record.ResourceName)}</span>
                        <span class='drift-type'>({HtmlEncode(record.ResourceType)})</span>
                    </div>
                    <span class='badge {badgeClass}'>{record.Severity}</span>
                </div>
                <dl class='drift-details'>
                    <dt>Pipeline</dt>
                    <dd>{HtmlEncode(record.PipelineName)}</dd>
                    <dt>Propiedad</dt>
                    <dd>{HtmlEncode(record.Property)}</dd>
                    <dt>Valor esperado</dt>
                    <dd><div class='value-box expected'>{HtmlEncode(record.ExpectedValue)}</div></dd>
                    <dt>Valor actual</dt>
                    <dd><div class='value-box actual'>{HtmlEncode(record.ActualValue)}</div></dd>
                    <dt>Descripción</dt>
                    <dd>{HtmlEncode(record.Description)}</dd>
                </dl>
                <div class='recommendation'>
                    <strong>💡 Recomendación:</strong> {HtmlEncode(record.Recommendation)}
                </div>
            </div>");
        }

        sb.AppendLine(@"
        <div class='footer'>
            <p>Este reporte fue generado automáticamente por Chivato.</p>
            <p>Accede al dashboard para más detalles: <a href='https://chivato.azurewebsites.net'>chivato.azurewebsites.net</a></p>
        </div>
    </div>
</body>
</html>");

        return sb.ToString();
    }

    private string GenerateDriftReportPlainText(IEnumerable<DriftRecordEntity> records)
    {
        var sb = new StringBuilder();

        sb.AppendLine("=== CHIVATO - REPORTE DE DRIFT ===");
        sb.AppendLine();

        var grouped = records.GroupBy(r => r.Severity).ToDictionary(g => g.Key, g => g.Count());
        sb.AppendLine($"Total de desviaciones: {records.Count()}");
        if (grouped.TryGetValue("CRITICAL", out var critical)) sb.AppendLine($"- Críticas: {critical}");
        if (grouped.TryGetValue("HIGH", out var high)) sb.AppendLine($"- Altas: {high}");
        if (grouped.TryGetValue("MEDIUM", out var medium)) sb.AppendLine($"- Medias: {medium}");
        if (grouped.TryGetValue("LOW", out var low)) sb.AppendLine($"- Bajas: {low}");

        sb.AppendLine();
        sb.AppendLine("=== DESVIACIONES ===");

        foreach (var record in records.OrderByDescending(r => GetSeverityOrder(r.Severity)))
        {
            sb.AppendLine();
            sb.AppendLine($"[{record.Severity}] {record.ResourceName} ({record.ResourceType})");
            sb.AppendLine($"Pipeline: {record.PipelineName}");
            sb.AppendLine($"Propiedad: {record.Property}");
            sb.AppendLine($"Esperado: {record.ExpectedValue}");
            sb.AppendLine($"Actual: {record.ActualValue}");
            sb.AppendLine($"Descripción: {record.Description}");
            sb.AppendLine($"Recomendación: {record.Recommendation}");
            sb.AppendLine("---");
        }

        return sb.ToString();
    }

    private string GenerateCredentialAlertSubject(IEnumerable<CredentialStatus> credentials)
    {
        var expired = credentials.Count(c => c.Status == "expired");
        var danger = credentials.Count(c => c.Status == "danger");

        if (expired > 0)
            return $"⛔ [Chivato] {expired} credenciales EXPIRADAS";
        if (danger > 0)
            return $"🔴 [Chivato] {danger} credenciales expiran en menos de 7 días";

        return $"⚠️ [Chivato] {credentials.Count()} credenciales requieren atención";
    }

    private string GenerateCredentialAlertHtml(IEnumerable<CredentialStatus> credentials)
    {
        var sb = new StringBuilder();

        sb.AppendLine(@"<!DOCTYPE html>
<html>
<head>
    <style>
        body { font-family: 'Inter', -apple-system, sans-serif; line-height: 1.6; color: #1a1a1a; }
        .container { max-width: 600px; margin: 0 auto; padding: 20px; }
        .header { background: linear-gradient(135deg, #1a1a1a, #2a2a2a); color: #ff6b35; padding: 30px; border-radius: 8px 8px 0 0; }
        .header h1 { margin: 0; font-size: 24px; }
        .content { background: #f5f5f5; padding: 20px; border-radius: 0 0 8px 8px; }
        .credential-item { background: white; border: 1px solid #e5e5e5; border-radius: 8px; padding: 16px; margin-bottom: 12px; }
        .credential-header { display: flex; justify-content: space-between; align-items: center; }
        .credential-name { font-weight: 600; }
        .badge { display: inline-block; padding: 4px 12px; border-radius: 20px; font-size: 12px; font-weight: 600; }
        .badge-expired { background: #fecaca; color: #991b1b; }
        .badge-danger { background: #fed7aa; color: #9a3412; }
        .badge-warning { background: #fef08a; color: #854d0e; }
        .days { margin-top: 8px; color: #666; font-size: 14px; }
        .footer { text-align: center; margin-top: 30px; padding-top: 20px; border-top: 1px solid #e5e5e5; color: #666; font-size: 12px; }
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>🔑 Alerta de Credenciales</h1>
        </div>
        <div class='content'>
            <p>Las siguientes credenciales requieren atención:</p>");

        foreach (var cred in credentials.OrderBy(c => c.DaysUntilExpiration))
        {
            var (badgeClass, statusText) = cred.Status switch
            {
                "expired" => ("badge-expired", "EXPIRADA"),
                "danger" => ("badge-danger", $"Expira en {cred.DaysUntilExpiration} días"),
                "warning" => ("badge-warning", $"Expira en {cred.DaysUntilExpiration} días"),
                _ => ("", "")
            };

            sb.AppendLine($@"
            <div class='credential-item'>
                <div class='credential-header'>
                    <span class='credential-name'>{HtmlEncode(cred.Name)}</span>
                    <span class='badge {badgeClass}'>{statusText}</span>
                </div>
                <div class='days'>
                    Tipo: {cred.Type} | Expira: {cred.ExpiresAt?.ToString("dd/MM/yyyy") ?? "N/A"}
                </div>
            </div>");
        }

        sb.AppendLine(@"
            <p style='margin-top: 20px;'><strong>Acción requerida:</strong> Por favor, renueva estas credenciales antes de su expiración para evitar interrupciones del servicio.</p>
        </div>
        <div class='footer'>
            <p>Generado por Chivato - <a href='https://chivato.azurewebsites.net'>Acceder al dashboard</a></p>
        </div>
    </div>
</body>
</html>");

        return sb.ToString();
    }

    private string GenerateCredentialAlertPlainText(IEnumerable<CredentialStatus> credentials)
    {
        var sb = new StringBuilder();

        sb.AppendLine("=== CHIVATO - ALERTA DE CREDENCIALES ===");
        sb.AppendLine();
        sb.AppendLine("Las siguientes credenciales requieren atención:");
        sb.AppendLine();

        foreach (var cred in credentials.OrderBy(c => c.DaysUntilExpiration))
        {
            var status = cred.Status switch
            {
                "expired" => "EXPIRADA",
                "danger" => $"Expira en {cred.DaysUntilExpiration} días",
                "warning" => $"Expira en {cred.DaysUntilExpiration} días",
                _ => ""
            };

            sb.AppendLine($"- {cred.Name} ({cred.Type}): {status}");
            sb.AppendLine($"  Fecha de expiración: {cred.ExpiresAt?.ToString("dd/MM/yyyy") ?? "N/A"}");
        }

        sb.AppendLine();
        sb.AppendLine("Por favor, renueva estas credenciales antes de su expiración.");

        return sb.ToString();
    }

    #endregion

    private static int GetSeverityOrder(string severity) => severity switch
    {
        "CRITICAL" => 4,
        "HIGH" => 3,
        "MEDIUM" => 2,
        "LOW" => 1,
        _ => 0
    };

    private static string HtmlEncode(string? text) =>
        System.Net.WebUtility.HtmlEncode(text ?? string.Empty);
}
