using Chivato.Shared.Models;
using Chivato.Shared.Services;
using Microsoft.AspNetCore.Mvc;

namespace Chivato.Api.Controllers;

[ApiController]
[Route("api/config")]
public class ConfigurationController : ControllerBase
{
    private readonly IStorageService _storageService;
    private readonly IKeyVaultService _keyVaultService;
    private readonly ILogger<ConfigurationController> _logger;

    public ConfigurationController(
        IStorageService storageService,
        IKeyVaultService keyVaultService,
        ILogger<ConfigurationController> logger)
    {
        _storageService = storageService;
        _keyVaultService = keyVaultService;
        _logger = logger;
    }

    // Timer Configuration
    [HttpGet("timer")]
    public async Task<IActionResult> GetTimerConfig()
    {
        var interval = await _storageService.GetConfigValueAsync("timer_interval");
        var enabled = await _storageService.GetConfigValueAsync("timer_enabled");

        return Ok(new
        {
            intervalHours = int.TryParse(interval, out var h) ? h : 24,
            isEnabled = bool.TryParse(enabled, out var e) && e
        });
    }

    [HttpPut("timer")]
    public async Task<IActionResult> UpdateTimerConfig([FromBody] TimerConfigInput input)
    {
        await _storageService.SetConfigValueAsync("timer_interval", input.IntervalHours.ToString(), "int");
        await _storageService.SetConfigValueAsync("timer_enabled", input.IsEnabled.ToString(), "bool");

        return Ok(new { success = true });
    }

    // Azure Connections
    [HttpGet("azure")]
    public async Task<IActionResult> GetAzureConnections()
    {
        var connections = await _storageService.GetAzureConnectionsAsync();
        var result = new List<object>();

        foreach (var conn in connections)
        {
            var expiration = !string.IsNullOrEmpty(conn.KeyVaultSecretName)
                ? await _keyVaultService.GetSecretExpirationAsync(conn.KeyVaultSecretName)
                : null;

            result.Add(new
            {
                id = conn.RowKey,
                name = conn.Name,
                tenantId = conn.TenantId,
                clientId = conn.ClientId,
                subscriptionIds = conn.SubscriptionIds,
                status = GetCredentialStatus(expiration),
                expiresAt = expiration
            });
        }

        return Ok(result);
    }

    [HttpPost("azure")]
    public async Task<IActionResult> CreateAzureConnection([FromBody] CreateAzureConnectionInput input)
    {
        var id = Guid.NewGuid().ToString();
        var secretName = $"azure-conn-{id}";

        // Store secret in Key Vault
        await _keyVaultService.SetSecretAsync(secretName, input.ClientSecret, input.ExpiresAt);

        var entity = new AzureConnectionEntity
        {
            RowKey = id,
            Name = input.Name,
            TenantId = input.TenantId,
            ClientId = input.ClientId,
            KeyVaultSecretName = secretName,
            SubscriptionIds = System.Text.Json.JsonSerializer.Serialize(input.SubscriptionIds ?? []),
            Status = "active",
            ExpiresAt = input.ExpiresAt
        };

        await _storageService.SaveAzureConnectionAsync(entity);

        _logger.LogInformation("Created Azure connection: {Name}", input.Name);

        return CreatedAtAction(nameof(GetAzureConnections), new { id }, new { id, name = input.Name });
    }

    [HttpDelete("azure/{id}")]
    public async Task<IActionResult> DeleteAzureConnection(string id)
    {
        var connection = await _storageService.GetAzureConnectionAsync(id);
        if (connection == null)
            return NotFound();

        // Delete secret from Key Vault
        if (!string.IsNullOrEmpty(connection.KeyVaultSecretName))
        {
            await _keyVaultService.DeleteSecretAsync(connection.KeyVaultSecretName);
        }

        await _storageService.DeleteAzureConnectionAsync(id);

        return NoContent();
    }

    // ADO Connections
    [HttpGet("ado")]
    public async Task<IActionResult> GetAdoConnections()
    {
        var connections = await _storageService.GetAdoConnectionsAsync();
        var result = new List<object>();

        foreach (var conn in connections)
        {
            var expiration = !string.IsNullOrEmpty(conn.KeyVaultSecretName)
                ? await _keyVaultService.GetSecretExpirationAsync(conn.KeyVaultSecretName)
                : null;

            result.Add(new
            {
                id = conn.RowKey,
                name = conn.Name,
                organizationUrl = conn.OrganizationUrl,
                authType = conn.AuthType,
                status = GetCredentialStatus(expiration),
                expiresAt = expiration
            });
        }

        return Ok(result);
    }

    [HttpPost("ado")]
    public async Task<IActionResult> CreateAdoConnection([FromBody] CreateAdoConnectionInput input)
    {
        var id = Guid.NewGuid().ToString();
        var secretName = $"ado-conn-{id}";

        // Store PAT in Key Vault
        await _keyVaultService.SetSecretAsync(secretName, input.Pat, input.ExpiresAt);

        var entity = new AdoConnectionEntity
        {
            RowKey = id,
            Name = input.Name,
            OrganizationUrl = input.OrganizationUrl,
            AuthType = "PAT",
            KeyVaultSecretName = secretName,
            Status = "active",
            ExpiresAt = input.ExpiresAt
        };

        await _storageService.SaveAdoConnectionAsync(entity);

        _logger.LogInformation("Created ADO connection: {Name}", input.Name);

        return CreatedAtAction(nameof(GetAdoConnections), new { id }, new { id, name = input.Name });
    }

    [HttpDelete("ado/{id}")]
    public async Task<IActionResult> DeleteAdoConnection(string id)
    {
        var connection = await _storageService.GetAdoConnectionAsync(id);
        if (connection == null)
            return NotFound();

        if (!string.IsNullOrEmpty(connection.KeyVaultSecretName))
        {
            await _keyVaultService.DeleteSecretAsync(connection.KeyVaultSecretName);
        }

        await _storageService.DeleteAdoConnectionAsync(id);

        return NoContent();
    }

    // Email Recipients
    [HttpGet("recipients")]
    public async Task<IActionResult> GetRecipients()
    {
        var recipients = await _storageService.GetEmailRecipientsAsync(activeOnly: false);
        return Ok(recipients.Select(r => new
        {
            id = r.RowKey,
            email = r.Email,
            notifyOn = r.NotifyOn,
            isActive = r.IsActive
        }));
    }

    [HttpPost("recipients")]
    public async Task<IActionResult> AddRecipient([FromBody] AddRecipientInput input)
    {
        var entity = new EmailRecipientEntity
        {
            RowKey = Guid.NewGuid().ToString(),
            Email = input.Email,
            NotifyOn = input.NotifyOn ?? "always",
            IsActive = true
        };

        await _storageService.SaveEmailRecipientAsync(entity);

        return CreatedAtAction(nameof(GetRecipients), new { id = entity.RowKey }, new { id = entity.RowKey, email = entity.Email });
    }

    [HttpDelete("recipients/{id}")]
    public async Task<IActionResult> DeleteRecipient(string id)
    {
        await _storageService.DeleteEmailRecipientAsync(id);
        return NoContent();
    }

    // AI Configuration
    [HttpGet("ai")]
    public async Task<IActionResult> GetAiConfig()
    {
        var connection = await _storageService.GetActiveAiConnectionAsync();
        if (connection == null)
            return Ok(new { configured = false });

        return Ok(new
        {
            configured = true,
            id = connection.RowKey,
            name = connection.Name,
            endpoint = connection.Endpoint,
            deploymentName = connection.DeploymentName,
            status = connection.Status
        });
    }

    [HttpPost("ai")]
    public async Task<IActionResult> SaveAiConfig([FromBody] SaveAiConfigInput input)
    {
        var id = Guid.NewGuid().ToString();
        var secretName = $"ai-conn-{id}";

        await _keyVaultService.SetSecretAsync(secretName, input.ApiKey);

        var entity = new AiConnectionEntity
        {
            RowKey = id,
            Name = input.Name ?? "Azure OpenAI",
            Endpoint = input.Endpoint,
            DeploymentName = input.DeploymentName,
            AuthType = "ApiKey",
            KeyVaultSecretName = secretName,
            Status = "active"
        };

        await _storageService.SaveAiConnectionAsync(entity);

        return Ok(new { id, success = true });
    }

    private static string GetCredentialStatus(DateTimeOffset? expiresAt)
    {
        if (!expiresAt.HasValue) return "active";

        var daysUntilExpiration = (expiresAt.Value - DateTimeOffset.UtcNow).TotalDays;

        return daysUntilExpiration switch
        {
            < 0 => "expired",
            < 14 => "danger",
            < 30 => "warning",
            _ => "active"
        };
    }
}

// Input DTOs
public record TimerConfigInput(int IntervalHours, bool IsEnabled);
public record CreateAzureConnectionInput(
    string Name,
    string TenantId,
    string ClientId,
    string ClientSecret,
    string[]? SubscriptionIds,
    DateTimeOffset? ExpiresAt);
public record CreateAdoConnectionInput(
    string Name,
    string OrganizationUrl,
    string Pat,
    DateTimeOffset? ExpiresAt);
public record AddRecipientInput(string Email, string? NotifyOn);
public record SaveAiConfigInput(string Endpoint, string DeploymentName, string ApiKey, string? Name);
