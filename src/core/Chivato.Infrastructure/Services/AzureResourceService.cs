using System.Text.Json;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Resources;
using Chivato.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace Chivato.Infrastructure.Services;

public class AzureResourceService : IAzureResourceService
{
    private readonly ArmClient _armClient;
    private readonly ILogger<AzureResourceService> _logger;

    public AzureResourceService(ArmClient armClient, ILogger<AzureResourceService> logger)
    {
        _armClient = armClient;
        _logger = logger;
    }

    public async Task<IReadOnlyList<AzureResource>> GetResourcesInGroupAsync(
        string subscriptionId,
        string resourceGroup,
        CancellationToken ct = default)
    {
        try
        {
            var resources = new List<AzureResource>();
            var subscription = _armClient.GetSubscriptionResource(new Azure.Core.ResourceIdentifier($"/subscriptions/{subscriptionId}"));
            var rg = await subscription.GetResourceGroupAsync(resourceGroup, ct);

            await foreach (var resource in rg.Value.GetGenericResourcesAsync(cancellationToken: ct))
            {
                var tags = resource.Data.Tags?.ToDictionary(t => t.Key, t => t.Value)
                    ?? new Dictionary<string, string>();

                var properties = new Dictionary<string, object>();

                // Try to get properties from Data
                if (resource.Data.Properties != null)
                {
                    try
                    {
                        var json = resource.Data.Properties.ToObjectFromJson<JsonElement>();
                        properties = FlattenJsonElement(json);
                    }
                    catch
                    {
                        // Properties may not be available for all resource types
                    }
                }

                resources.Add(new AzureResource(
                    Id: resource.Data.Id?.ToString() ?? "",
                    Name: resource.Data.Name,
                    Type: resource.Data.ResourceType.ToString(),
                    ResourceGroup: resourceGroup,
                    Location: resource.Data.Location.Name,
                    Tags: tags,
                    Properties: properties
                ));
            }

            _logger.LogInformation("Retrieved {Count} resources from {ResourceGroup}", resources.Count, resourceGroup);
            return resources;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting resources from {ResourceGroup}", resourceGroup);
            throw;
        }
    }

    public async Task<AzureResource?> GetResourceAsync(string resourceId, CancellationToken ct = default)
    {
        try
        {
            var resource = _armClient.GetGenericResource(new Azure.Core.ResourceIdentifier(resourceId));
            var response = await resource.GetAsync(ct);

            var data = response.Value.Data;
            var tags = data.Tags?.ToDictionary(t => t.Key, t => t.Value)
                ?? new Dictionary<string, string>();

            var properties = new Dictionary<string, object>();
            if (data.Properties != null)
            {
                try
                {
                    var json = data.Properties.ToObjectFromJson<JsonElement>();
                    properties = FlattenJsonElement(json);
                }
                catch
                {
                    // Properties may not be available
                }
            }

            return new AzureResource(
                Id: data.Id?.ToString() ?? "",
                Name: data.Name,
                Type: data.ResourceType.ToString(),
                ResourceGroup: ExtractResourceGroup(resourceId),
                Location: data.Location.Name,
                Tags: tags,
                Properties: properties
            );
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            _logger.LogWarning("Resource not found: {ResourceId}", resourceId);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting resource {ResourceId}", resourceId);
            throw;
        }
    }

    public async Task<IReadOnlyDictionary<string, object>> GetResourcePropertiesAsync(
        string resourceId,
        CancellationToken ct = default)
    {
        var resource = await GetResourceAsync(resourceId, ct);
        return resource?.Properties ?? new Dictionary<string, object>();
    }

    private static string ExtractResourceGroup(string resourceId)
    {
        var parts = resourceId.Split('/');
        for (int i = 0; i < parts.Length - 1; i++)
        {
            if (parts[i].Equals("resourceGroups", StringComparison.OrdinalIgnoreCase))
            {
                return parts[i + 1];
            }
        }
        return "";
    }

    private static Dictionary<string, object> FlattenJsonElement(JsonElement element, string prefix = "")
    {
        var result = new Dictionary<string, object>();

        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var prop in element.EnumerateObject())
                {
                    var key = string.IsNullOrEmpty(prefix) ? prop.Name : $"{prefix}.{prop.Name}";
                    foreach (var item in FlattenJsonElement(prop.Value, key))
                    {
                        result[item.Key] = item.Value;
                    }
                }
                break;

            case JsonValueKind.Array:
                result[prefix] = element.GetRawText();
                break;

            case JsonValueKind.String:
                result[prefix] = element.GetString() ?? "";
                break;

            case JsonValueKind.Number:
                result[prefix] = element.GetDouble();
                break;

            case JsonValueKind.True:
            case JsonValueKind.False:
                result[prefix] = element.GetBoolean();
                break;

            case JsonValueKind.Null:
                result[prefix] = null!;
                break;
        }

        return result;
    }
}
