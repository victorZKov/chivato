using Chivato.Shared.Models;

namespace Chivato.Shared.Services;

/// <summary>
/// Email notification service
/// </summary>
public interface IEmailService
{
    /// <summary>
    /// Send drift report email
    /// </summary>
    Task SendDriftReportAsync(
        IEnumerable<string> recipients,
        string pipelineName,
        DriftAnalysisResult result,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Send credential expiration warning
    /// </summary>
    Task SendCredentialExpirationWarningAsync(
        IEnumerable<string> recipients,
        IEnumerable<CredentialStatus> expiringCredentials,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Test email configuration
    /// </summary>
    Task<bool> TestConnectionAsync(string connectionString, string fromEmail);
}
