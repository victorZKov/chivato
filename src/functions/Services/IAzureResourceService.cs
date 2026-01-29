using Chivato.Functions.Models;

namespace Chivato.Functions.Services;

public interface IAzureResourceService
{
    Task<bool> TestConnectionAsync(string tenantId, string clientId, string clientSecret);
    Task<IEnumerable<string>> GetSubscriptionsAsync(string tenantId, string clientId, string clientSecret);
    Task<IEnumerable<string>> GetResourceGroupsAsync(string subscriptionId, string tenantId, string clientId, string clientSecret);
    Task<IEnumerable<AzureResourceState>> GetResourcesAsync(string subscriptionId, string resourceGroup, string tenantId, string clientId, string clientSecret);
}
