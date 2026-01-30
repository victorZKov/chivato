using Chivato.Domain.Entities;

namespace Chivato.Domain.Interfaces;

public interface ICredentialRepository
{
    Task<Credential?> GetByIdAsync(string tenantId, string id, CancellationToken ct = default);
    Task<IReadOnlyList<Credential>> GetAllAsync(string tenantId, CancellationToken ct = default);
    Task<IReadOnlyList<Credential>> GetByTypeAsync(string tenantId, CredentialType type, CancellationToken ct = default);
    Task<IReadOnlyList<Credential>> GetExpiringAsync(string tenantId, int daysThreshold = 7, CancellationToken ct = default);
    Task<IReadOnlyList<Credential>> GetExpiredAsync(string tenantId, CancellationToken ct = default);
    Task<Credential?> GetByKeyVaultNameAsync(string tenantId, string keyVaultSecretName, CancellationToken ct = default);
    Task AddAsync(Credential credential, CancellationToken ct = default);
    Task UpdateAsync(Credential credential, CancellationToken ct = default);
    Task DeleteAsync(string tenantId, string id, CancellationToken ct = default);
}
