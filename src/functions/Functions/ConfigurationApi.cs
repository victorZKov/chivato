using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;
using Chivato.Functions.Services;
using Chivato.Functions.Models;

namespace Chivato.Functions.Functions;

public class ConfigurationApi
{
    private readonly ILogger<ConfigurationApi> _logger;
    private readonly IStorageService _storageService;
    private readonly IKeyVaultService _keyVaultService;

    public ConfigurationApi(
        ILogger<ConfigurationApi> logger,
        IStorageService storageService,
        IKeyVaultService keyVaultService)
    {
        _logger = logger;
        _storageService = storageService;
        _keyVaultService = keyVaultService;
    }

    [Function("GetConfiguration")]
    public async Task<HttpResponseData> GetConfiguration(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "config")] HttpRequestData req)
    {
        _logger.LogInformation("Getting configuration");

        var timerInterval = await _storageService.GetConfigValueAsync("timer_interval") ?? "24";
        var retentionDays = await _storageService.GetConfigValueAsync("retention_days") ?? "90";

        var config = new
        {
            timerInterval = int.Parse(timerInterval),
            retentionDays = int.Parse(retentionDays)
        };

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(config);
        return response;
    }

    [Function("UpdateConfiguration")]
    public async Task<HttpResponseData> UpdateConfiguration(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "config")] HttpRequestData req)
    {
        _logger.LogInformation("Updating configuration");

        var body = await req.ReadAsStringAsync();
        var config = JsonSerializer.Deserialize<Dictionary<string, int>>(body ?? "{}");

        if (config != null)
        {
            if (config.TryGetValue("timerInterval", out var timerInterval))
            {
                await _storageService.SetConfigValueAsync("timer_interval", timerInterval.ToString(), "hours");
            }

            if (config.TryGetValue("retentionDays", out var retentionDays))
            {
                await _storageService.SetConfigValueAsync("retention_days", retentionDays.ToString(), "days");
            }
        }

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new { success = true });
        return response;
    }

    [Function("GetAzureConnections")]
    public async Task<HttpResponseData> GetAzureConnections(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "connections/azure")] HttpRequestData req)
    {
        var connections = await _storageService.GetAzureConnectionsAsync();

        // Don't return secrets, only metadata
        var result = connections.Select(c => new
        {
            id = c.RowKey,
            name = c.Name,
            tenantId = c.TenantId,
            clientId = c.ClientId,
            subscriptionIds = JsonSerializer.Deserialize<List<string>>(c.SubscriptionIds),
            status = c.Status,
            expiresAt = c.ExpiresAt
        });

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(result);
        return response;
    }

    [Function("CreateAzureConnection")]
    public async Task<HttpResponseData> CreateAzureConnection(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "connections/azure")] HttpRequestData req)
    {
        var body = await req.ReadAsStringAsync();
        var input = JsonSerializer.Deserialize<AzureConnectionInput>(body ?? "{}");

        if (input == null)
        {
            var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
            await badRequest.WriteAsJsonAsync(new { error = "Invalid input" });
            return badRequest;
        }

        var connectionId = Guid.NewGuid().ToString();
        var secretName = $"azure-conn-{connectionId}";

        // Store secret in Key Vault
        await _keyVaultService.SetSecretAsync(secretName, input.ClientSecret, input.ExpiresAt);

        // Store connection metadata in Table Storage
        var connection = new AzureConnectionEntity
        {
            RowKey = connectionId,
            Name = input.Name,
            TenantId = input.TenantId,
            ClientId = input.ClientId,
            KeyVaultSecretName = secretName,
            SubscriptionIds = JsonSerializer.Serialize(input.SubscriptionIds),
            Status = "active",
            ExpiresAt = input.ExpiresAt
        };

        await _storageService.SaveAzureConnectionAsync(connection);

        var response = req.CreateResponse(HttpStatusCode.Created);
        await response.WriteAsJsonAsync(new { id = connectionId });
        return response;
    }

    [Function("DeleteAzureConnection")]
    public async Task<HttpResponseData> DeleteAzureConnection(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "connections/azure/{id}")] HttpRequestData req,
        string id)
    {
        var connection = await _storageService.GetAzureConnectionAsync(id);
        if (connection != null)
        {
            await _keyVaultService.DeleteSecretAsync(connection.KeyVaultSecretName);
            await _storageService.DeleteAzureConnectionAsync(id);
        }

        var response = req.CreateResponse(HttpStatusCode.NoContent);
        return response;
    }

    [Function("GetEmailRecipients")]
    public async Task<HttpResponseData> GetEmailRecipients(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "recipients")] HttpRequestData req)
    {
        var recipients = await _storageService.GetEmailRecipientsAsync(activeOnly: false);

        var result = recipients.Select(r => new
        {
            id = r.RowKey,
            email = r.Email,
            notifyOn = r.NotifyOn,
            isActive = r.IsActive
        });

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(result);
        return response;
    }

    [Function("AddEmailRecipient")]
    public async Task<HttpResponseData> AddEmailRecipient(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "recipients")] HttpRequestData req)
    {
        var body = await req.ReadAsStringAsync();
        var input = JsonSerializer.Deserialize<EmailRecipientInput>(body ?? "{}");

        if (input == null || string.IsNullOrEmpty(input.Email))
        {
            var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
            await badRequest.WriteAsJsonAsync(new { error = "Invalid email" });
            return badRequest;
        }

        var recipient = new EmailRecipientEntity
        {
            Email = input.Email,
            NotifyOn = input.NotifyOn ?? "always",
            IsActive = true
        };

        await _storageService.SaveEmailRecipientAsync(recipient);

        var response = req.CreateResponse(HttpStatusCode.Created);
        await response.WriteAsJsonAsync(new { id = recipient.RowKey });
        return response;
    }
}

// Input DTOs
public record AzureConnectionInput(
    string Name,
    string TenantId,
    string ClientId,
    string ClientSecret,
    List<string> SubscriptionIds,
    DateTimeOffset? ExpiresAt);

public record EmailRecipientInput(string Email, string? NotifyOn);
