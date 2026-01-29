using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Resources;
using Chivato.Shared.Models;

namespace Chivato.Shared.Services;

/// <summary>
/// Azure Resource Manager service for reading resource states
/// </summary>
public class AzureResourceService : IAzureResourceService
{
    public async Task<bool> TestConnectionAsync(string tenantId, string clientId, string clientSecret)
    {
        try
        {
            var credential = new ClientSecretCredential(tenantId, clientId, clientSecret);
            var client = new ArmClient(credential);

            // Try to list subscriptions - if this works, connection is valid
            await foreach (var subscription in client.GetSubscriptions().GetAllAsync())
            {
                return true;
            }
            return true; // No subscriptions but connection worked
        }
        catch
        {
            return false;
        }
    }

    public async Task<IEnumerable<AzureSubscription>> GetSubscriptionsAsync(string tenantId, string clientId, string clientSecret)
    {
        var credential = new ClientSecretCredential(tenantId, clientId, clientSecret);
        var client = new ArmClient(credential);

        var subscriptions = new List<AzureSubscription>();

        await foreach (var subscription in client.GetSubscriptions().GetAllAsync())
        {
            subscriptions.Add(new AzureSubscription
            {
                Id = subscription.Data.SubscriptionId ?? string.Empty,
                Name = subscription.Data.DisplayName ?? string.Empty,
                State = subscription.Data.State?.ToString() ?? "Unknown"
            });
        }

        return subscriptions;
    }

    public async Task<IEnumerable<AzureResourceGroup>> GetResourceGroupsAsync(
        string tenantId, string clientId, string clientSecret, string subscriptionId)
    {
        var credential = new ClientSecretCredential(tenantId, clientId, clientSecret);
        var client = new ArmClient(credential);

        var subscription = client.GetSubscriptionResource(
            SubscriptionResource.CreateResourceIdentifier(subscriptionId));

        var resourceGroups = new List<AzureResourceGroup>();

        await foreach (var rg in subscription.GetResourceGroups().GetAllAsync())
        {
            resourceGroups.Add(new AzureResourceGroup
            {
                Name = rg.Data.Name ?? string.Empty,
                Location = rg.Data.Location.DisplayName ?? string.Empty
            });
        }

        return resourceGroups;
    }

    public async Task<IEnumerable<AzureResourceState>> GetResourcesAsync(
        string tenantId, string clientId, string clientSecret,
        string subscriptionId, string resourceGroup,
        CancellationToken cancellationToken = default)
    {
        var credential = new ClientSecretCredential(tenantId, clientId, clientSecret);
        var client = new ArmClient(credential);

        var subscription = client.GetSubscriptionResource(
            SubscriptionResource.CreateResourceIdentifier(subscriptionId));
        var rgResponse = await subscription.GetResourceGroupAsync(resourceGroup, cancellationToken);
        var rgResource = rgResponse.Value;

        var resources = new List<AzureResourceState>();

        await foreach (var resource in rgResource.GetGenericResourcesAsync(cancellationToken: cancellationToken))
        {
            var state = new AzureResourceState
            {
                ResourceId = resource.Id.ToString(),
                ResourceType = resource.Data.ResourceType.ToString(),
                Name = resource.Data.Name,
                ResourceGroup = resourceGroup,
                SubscriptionId = subscriptionId,
                Location = resource.Data.Location.DisplayName ?? string.Empty,
                Tags = resource.Data.Tags?.ToDictionary(t => t.Key, t => t.Value) ?? new()
            };

            // Get resource properties if available
            try
            {
                var fullResource = await resource.GetAsync(cancellationToken);
                if (fullResource.Value.Data.Properties != null)
                {
                    state.Properties = System.Text.Json.JsonSerializer
                        .Deserialize<Dictionary<string, object>>(
                            fullResource.Value.Data.Properties.ToString()) ?? new();
                }
            }
            catch
            {
                // Some resources don't expose properties - continue without them
            }

            resources.Add(state);
        }

        return resources;
    }
}
