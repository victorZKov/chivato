using Chivato.Domain.Entities;

namespace Chivato.Domain.Interfaces;

public interface IAzureConnectionRepository
{
    Task<AzureConnection?> GetByIdAsync(string tenantId, string id, CancellationToken ct = default);
    Task<IReadOnlyList<AzureConnection>> GetAllAsync(string tenantId, CancellationToken ct = default);
    Task<AzureConnection?> GetDefaultAsync(string tenantId, CancellationToken ct = default);
    Task<AzureConnection?> GetBySubscriptionIdAsync(string tenantId, string subscriptionId, CancellationToken ct = default);
    Task AddAsync(AzureConnection connection, CancellationToken ct = default);
    Task UpdateAsync(AzureConnection connection, CancellationToken ct = default);
    Task DeleteAsync(string tenantId, string id, CancellationToken ct = default);
}
