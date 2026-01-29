using Chivato.Functions.Models;

namespace Chivato.Functions.Services;

public interface IEmailService
{
    Task<bool> TestConnectionAsync(string connectionString, string fromEmail);
    Task SendDriftReportAsync(
        IEnumerable<DriftRecordEntity> driftRecords,
        IEnumerable<string> recipients,
        string connectionString,
        string fromEmail,
        string fromDisplayName);
    Task SendCredentialExpirationAlertAsync(
        IEnumerable<CredentialStatus> expiringCredentials,
        IEnumerable<string> recipients,
        string connectionString,
        string fromEmail,
        string fromDisplayName);
}
