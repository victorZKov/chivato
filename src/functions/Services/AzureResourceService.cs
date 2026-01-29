using Microsoft.Extensions.Logging;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Resources;
using Chivato.Functions.Models;
using System.Text.Json;

namespace Chivato.Functions.Services;

public class AzureResourceService : IAzureResourceService
{
    private readonly ILogger<AzureResourceService> _logger;

    public AzureResourceService(ILogger<AzureResourceService> logger)
    {
        _logger = logger;
    }

    private ArmClient GetArmClient(string tenantId, string clientId, string clientSecret)
    {
        var credential = new ClientSecretCredential(tenantId, clientId, clientSecret);
        return new ArmClient(credential);
    }

    public async Task<bool> TestConnectionAsync(string tenantId, string clientId, string clientSecret)
    {
        try
        {
            var client = GetArmClient(tenantId, clientId, clientSecret);
            var subscriptions = client.GetSubscriptions();

            var count = 0;
            await foreach (var sub in subscriptions)
            {
                count++;
                if (count >= 1) break; // Just verify we can access at least one
            }

            _logger.LogInformation("Azure connection test successful. Found subscriptions.");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Azure connection test failed for tenant {TenantId}", tenantId);
            return false;
        }
    }

    public async Task<IEnumerable<string>> GetSubscriptionsAsync(
        string tenantId, string clientId, string clientSecret)
    {
        try
        {
            var client = GetArmClient(tenantId, clientId, clientSecret);
            var subscriptions = new List<string>();

            await foreach (var sub in client.GetSubscriptions())
            {
                subscriptions.Add(sub.Data.SubscriptionId);
            }

            _logger.LogInformation("Found {Count} Azure subscriptions", subscriptions.Count);
            return subscriptions;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get Azure subscriptions");
            throw;
        }
    }

    public async Task<IEnumerable<string>> GetResourceGroupsAsync(
        string subscriptionId, string tenantId, string clientId, string clientSecret)
    {
        try
        {
            var client = GetArmClient(tenantId, clientId, clientSecret);
            var subscription = client.GetSubscriptionResource(
                new Azure.Core.ResourceIdentifier($"/subscriptions/{subscriptionId}"));

            var resourceGroups = new List<string>();
            await foreach (var rg in subscription.GetResourceGroups())
            {
                resourceGroups.Add(rg.Data.Name);
            }

            _logger.LogInformation("Found {Count} resource groups in subscription {SubscriptionId}",
                resourceGroups.Count, subscriptionId);
            return resourceGroups.OrderBy(n => n);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get resource groups for subscription {SubscriptionId}", subscriptionId);
            throw;
        }
    }

    public async Task<IEnumerable<AzureResourceState>> GetResourcesAsync(
        string subscriptionId, string resourceGroup,
        string tenantId, string clientId, string clientSecret)
    {
        var resources = new List<AzureResourceState>();

        try
        {
            var client = GetArmClient(tenantId, clientId, clientSecret);
            var subscription = client.GetSubscriptionResource(
                new Azure.Core.ResourceIdentifier($"/subscriptions/{subscriptionId}"));

            var rg = await subscription.GetResourceGroups().GetAsync(resourceGroup);

            await foreach (var resource in rg.Value.GetGenericResourcesAsync())
            {
                var resourceData = resource.Data;

                // Get full resource details
                var fullResource = await resource.GetAsync();
                var properties = new Dictionary<string, object>();
                var tags = new Dictionary<string, string>();

                // Extract properties from the resource
                if (fullResource.Value.Data.Properties != null)
                {
                    try
                    {
                        var propsJson = fullResource.Value.Data.Properties.ToObjectFromJson<Dictionary<string, object>>();
                        if (propsJson != null)
                        {
                            properties = FlattenProperties(propsJson);
                        }
                    }
                    catch
                    {
                        // Properties might not be deserializable to dictionary
                    }
                }

                // Extract tags
                if (resourceData.Tags != null)
                {
                    foreach (var tag in resourceData.Tags)
                    {
                        tags[tag.Key] = tag.Value;
                    }
                }

                resources.Add(new AzureResourceState
                {
                    ResourceId = resourceData.Id.ToString(),
                    ResourceType = resourceData.ResourceType.ToString(),
                    Name = resourceData.Name,
                    ResourceGroup = resourceGroup,
                    SubscriptionId = subscriptionId,
                    Location = resourceData.Location.Name ?? string.Empty,
                    Properties = properties,
                    Tags = tags
                });
            }

            _logger.LogInformation("Found {Count} resources in resource group {ResourceGroup}",
                resources.Count, resourceGroup);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get resources from {ResourceGroup}", resourceGroup);
            throw;
        }

        return resources;
    }

    private Dictionary<string, object> FlattenProperties(Dictionary<string, object> properties, string prefix = "")
    {
        var result = new Dictionary<string, object>();

        foreach (var kvp in properties)
        {
            var key = string.IsNullOrEmpty(prefix) ? kvp.Key : $"{prefix}.{kvp.Key}";

            if (kvp.Value is JsonElement jsonElement)
            {
                switch (jsonElement.ValueKind)
                {
                    case JsonValueKind.Object:
                        var nested = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonElement.GetRawText());
                        if (nested != null)
                        {
                            foreach (var nestedKvp in FlattenProperties(nested, key))
                            {
                                result[nestedKvp.Key] = nestedKvp.Value;
                            }
                        }
                        break;
                    case JsonValueKind.Array:
                        result[key] = jsonElement.GetRawText();
                        break;
                    case JsonValueKind.String:
                        result[key] = jsonElement.GetString() ?? string.Empty;
                        break;
                    case JsonValueKind.Number:
                        result[key] = jsonElement.GetDecimal();
                        break;
                    case JsonValueKind.True:
                    case JsonValueKind.False:
                        result[key] = jsonElement.GetBoolean();
                        break;
                    default:
                        result[key] = jsonElement.GetRawText();
                        break;
                }
            }
            else if (kvp.Value is Dictionary<string, object> dict)
            {
                foreach (var nestedKvp in FlattenProperties(dict, key))
                {
                    result[nestedKvp.Key] = nestedKvp.Value;
                }
            }
            else
            {
                result[key] = kvp.Value ?? string.Empty;
            }
        }

        return result;
    }
}
