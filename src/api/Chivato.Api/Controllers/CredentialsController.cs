using Chivato.Shared.Models;
using Chivato.Shared.Services;
using Microsoft.AspNetCore.Mvc;

namespace Chivato.Api.Controllers;

[ApiController]
[Route("api/credentials")]
public class CredentialsController : ControllerBase
{
    private readonly IStorageService _storageService;
    private readonly IKeyVaultService _keyVaultService;
    private readonly ILogger<CredentialsController> _logger;

    public CredentialsController(
        IStorageService storageService,
        IKeyVaultService keyVaultService,
        ILogger<CredentialsController> logger)
    {
        _storageService = storageService;
        _keyVaultService = keyVaultService;
        _logger = logger;
    }

    /// <summary>
    /// Get all credentials with their status
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetCredentials()
    {
        var credentials = new List<CredentialStatus>();

        // Azure connections
        var azureConnections = await _storageService.GetAzureConnectionsAsync();
        foreach (var conn in azureConnections)
        {
            var expiration = conn.ExpiresAt;
            credentials.Add(new CredentialStatus
            {
                Id = conn.RowKey,
                Name = conn.Name,
                Type = "Azure",
                ExpiresAt = expiration,
                DaysUntilExpiration = expiration.HasValue
                    ? (int)(expiration.Value - DateTimeOffset.UtcNow).TotalDays
                    : -1,
                Status = GetStatusFromExpiration(expiration)
            });
        }

        // ADO connections
        var adoConnections = await _storageService.GetAdoConnectionsAsync();
        foreach (var conn in adoConnections)
        {
            var expiration = conn.ExpiresAt;
            credentials.Add(new CredentialStatus
            {
                Id = conn.RowKey,
                Name = conn.Name,
                Type = "ADO",
                ExpiresAt = expiration,
                DaysUntilExpiration = expiration.HasValue
                    ? (int)(expiration.Value - DateTimeOffset.UtcNow).TotalDays
                    : -1,
                Status = GetStatusFromExpiration(expiration)
            });
        }

        // AI connection
        var aiConnection = await _storageService.GetActiveAiConnectionAsync();
        if (aiConnection != null)
        {
            credentials.Add(new CredentialStatus
            {
                Id = aiConnection.RowKey,
                Name = aiConnection.Name,
                Type = "AI",
                Status = aiConnection.Status == "active" ? "ok" : "warning"
            });
        }

        // Email service
        var emailConfig = await _storageService.GetEmailServiceConfigAsync();
        if (emailConfig != null)
        {
            credentials.Add(new CredentialStatus
            {
                Id = emailConfig.RowKey,
                Name = "Email Service",
                Type = "Email",
                Status = emailConfig.IsActive ? "ok" : "warning"
            });
        }

        return Ok(credentials.Select(c => new
        {
            id = c.Id,
            name = c.Name,
            type = c.Type,
            expiresAt = c.ExpiresAt,
            daysUntilExpiration = c.DaysUntilExpiration,
            status = c.Status,
            lastTestedAt = c.LastTestedAt,
            lastTestResult = c.LastTestResult
        }));
    }

    /// <summary>
    /// Get credentials expiring soon (within 30 days)
    /// </summary>
    [HttpGet("expiring")]
    public async Task<IActionResult> GetExpiringCredentials([FromQuery] int days = 30)
    {
        var expiringThreshold = DateTimeOffset.UtcNow.AddDays(days);
        var credentials = new List<CredentialStatus>();

        var azureConnections = await _storageService.GetAzureConnectionsAsync();
        foreach (var conn in azureConnections.Where(c => c.ExpiresAt.HasValue && c.ExpiresAt.Value <= expiringThreshold))
        {
            credentials.Add(new CredentialStatus
            {
                Id = conn.RowKey,
                Name = conn.Name,
                Type = "Azure",
                ExpiresAt = conn.ExpiresAt,
                DaysUntilExpiration = (int)(conn.ExpiresAt!.Value - DateTimeOffset.UtcNow).TotalDays,
                Status = GetStatusFromExpiration(conn.ExpiresAt)
            });
        }

        var adoConnections = await _storageService.GetAdoConnectionsAsync();
        foreach (var conn in adoConnections.Where(c => c.ExpiresAt.HasValue && c.ExpiresAt.Value <= expiringThreshold))
        {
            credentials.Add(new CredentialStatus
            {
                Id = conn.RowKey,
                Name = conn.Name,
                Type = "ADO",
                ExpiresAt = conn.ExpiresAt,
                DaysUntilExpiration = (int)(conn.ExpiresAt!.Value - DateTimeOffset.UtcNow).TotalDays,
                Status = GetStatusFromExpiration(conn.ExpiresAt)
            });
        }

        return Ok(credentials.OrderBy(c => c.DaysUntilExpiration).Select(c => new
        {
            id = c.Id,
            name = c.Name,
            type = c.Type,
            expiresAt = c.ExpiresAt,
            daysUntilExpiration = c.DaysUntilExpiration,
            status = c.Status
        }));
    }

    /// <summary>
    /// Test a credential connection
    /// </summary>
    [HttpPost("{type}/{id}/test")]
    public async Task<IActionResult> TestCredential(string type, string id)
    {
        try
        {
            bool success = false;
            string? error = null;

            switch (type.ToLower())
            {
                case "azure":
                    var azureConn = await _storageService.GetAzureConnectionAsync(id);
                    if (azureConn == null) return NotFound();

                    var azureSecret = await _keyVaultService.GetSecretAsync(azureConn.KeyVaultSecretName);
                    if (string.IsNullOrEmpty(azureSecret))
                    {
                        error = "Secret not found in Key Vault";
                    }
                    else
                    {
                        // TODO: Actually test the connection
                        success = true;
                    }
                    break;

                case "ado":
                    var adoConn = await _storageService.GetAdoConnectionAsync(id);
                    if (adoConn == null) return NotFound();

                    var adoSecret = await _keyVaultService.GetSecretAsync(adoConn.KeyVaultSecretName);
                    if (string.IsNullOrEmpty(adoSecret))
                    {
                        error = "Secret not found in Key Vault";
                    }
                    else
                    {
                        // TODO: Actually test the connection
                        success = true;
                    }
                    break;

                case "ai":
                    var aiConn = await _storageService.GetActiveAiConnectionAsync();
                    if (aiConn == null || aiConn.RowKey != id) return NotFound();

                    var aiSecret = await _keyVaultService.GetSecretAsync(aiConn.KeyVaultSecretName);
                    success = !string.IsNullOrEmpty(aiSecret);
                    if (!success) error = "Secret not found in Key Vault";
                    break;

                default:
                    return BadRequest(new { error = "Invalid credential type" });
            }

            _logger.LogInformation("Credential test for {Type}/{Id}: {Result}", type, id, success ? "Success" : "Failed");

            return Ok(new
            {
                success,
                error,
                testedAt = DateTimeOffset.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing credential {Type}/{Id}", type, id);
            return Ok(new
            {
                success = false,
                error = ex.Message,
                testedAt = DateTimeOffset.UtcNow
            });
        }
    }

    /// <summary>
    /// Rotate a credential (update the secret)
    /// </summary>
    [HttpPut("{type}/{id}/rotate")]
    public async Task<IActionResult> RotateCredential(string type, string id, [FromBody] RotateCredentialInput input)
    {
        if (string.IsNullOrEmpty(input.NewSecret))
        {
            return BadRequest(new { error = "NewSecret is required" });
        }

        try
        {
            switch (type.ToLower())
            {
                case "azure":
                    var azureConn = await _storageService.GetAzureConnectionAsync(id);
                    if (azureConn == null) return NotFound();

                    await _keyVaultService.SetSecretAsync(azureConn.KeyVaultSecretName, input.NewSecret, input.ExpiresAt);

                    if (input.ExpiresAt.HasValue)
                    {
                        azureConn.ExpiresAt = input.ExpiresAt;
                        await _storageService.SaveAzureConnectionAsync(azureConn);
                    }
                    break;

                case "ado":
                    var adoConn = await _storageService.GetAdoConnectionAsync(id);
                    if (adoConn == null) return NotFound();

                    await _keyVaultService.SetSecretAsync(adoConn.KeyVaultSecretName, input.NewSecret, input.ExpiresAt);

                    if (input.ExpiresAt.HasValue)
                    {
                        adoConn.ExpiresAt = input.ExpiresAt;
                        await _storageService.SaveAdoConnectionAsync(adoConn);
                    }
                    break;

                case "ai":
                    var aiConn = await _storageService.GetActiveAiConnectionAsync();
                    if (aiConn == null || aiConn.RowKey != id) return NotFound();

                    await _keyVaultService.SetSecretAsync(aiConn.KeyVaultSecretName, input.NewSecret);
                    break;

                default:
                    return BadRequest(new { error = "Invalid credential type" });
            }

            _logger.LogInformation("Credential rotated: {Type}/{Id}", type, id);

            return Ok(new
            {
                success = true,
                rotatedAt = DateTimeOffset.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rotating credential {Type}/{Id}", type, id);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    private static string GetStatusFromExpiration(DateTimeOffset? expiresAt)
    {
        if (!expiresAt.HasValue) return "ok";

        var daysUntil = (expiresAt.Value - DateTimeOffset.UtcNow).TotalDays;

        if (daysUntil < 0) return "expired";
        if (daysUntil <= 7) return "danger";
        if (daysUntil <= 30) return "warning";
        return "ok";
    }
}

public record RotateCredentialInput(string NewSecret, DateTimeOffset? ExpiresAt);
