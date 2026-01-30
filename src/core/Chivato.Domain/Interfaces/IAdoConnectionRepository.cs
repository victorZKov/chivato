using Chivato.Domain.Entities;

namespace Chivato.Domain.Interfaces;

public interface IAdoConnectionRepository
{
    Task<AdoConnection?> GetByIdAsync(string tenantId, string id, CancellationToken ct = default);
    Task<IReadOnlyList<AdoConnection>> GetAllAsync(string tenantId, CancellationToken ct = default);
    Task<AdoConnection?> GetDefaultAsync(string tenantId, CancellationToken ct = default);
    Task<AdoConnection?> GetByOrganizationAsync(string tenantId, string organization, CancellationToken ct = default);
    Task AddAsync(AdoConnection connection, CancellationToken ct = default);
    Task UpdateAsync(AdoConnection connection, CancellationToken ct = default);
    Task DeleteAsync(string tenantId, string id, CancellationToken ct = default);
}
