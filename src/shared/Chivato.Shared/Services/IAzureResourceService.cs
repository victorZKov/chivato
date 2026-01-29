using Chivato.Shared.Models;

namespace Chivato.Shared.Services;

/// <summary>
/// Azure Resource Manager integration service
/// </summary>
public interface IAzureResourceService
{
    /// <summary>
    /// Test connection with Azure credentials
    /// </summary>
    Task<bool> TestConnectionAsync(string tenantId, string clientId, string clientSecret);

    /// <summary>
    /// Get list of subscriptions accessible with the credentials
    /// </summary>
    Task<IEnumerable<AzureSubscription>> GetSubscriptionsAsync(string tenantId, string clientId, string clientSecret);

    /// <summary>
    /// Get resource groups in a subscription
    /// </summary>
    Task<IEnumerable<AzureResourceGroup>> GetResourceGroupsAsync(
        string tenantId, string clientId, string clientSecret, string subscriptionId);

    /// <summary>
    /// Get current state of resources in a resource group
    /// </summary>
    Task<IEnumerable<AzureResourceState>> GetResourcesAsync(
        string tenantId, string clientId, string clientSecret,
        string subscriptionId, string resourceGroup,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Azure Subscription info
/// </summary>
public class AzureSubscription
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
}

/// <summary>
/// Azure Resource Group info
/// </summary>
public class AzureResourceGroup
{
    public string Name { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
}
