using Azure.Data.Tables;
using Chivato.Domain.Entities;
using Chivato.Domain.Interfaces;
using Chivato.Infrastructure.TableEntities;

namespace Chivato.Infrastructure.Repositories;

public class CredentialRepository : ICredentialRepository
{
    private readonly TableClient _tableClient;
    private const string TableName = "Credentials";

    public CredentialRepository(TableServiceClient tableServiceClient)
    {
        _tableClient = tableServiceClient.GetTableClient(TableName);
        _tableClient.CreateIfNotExists();
    }

    public async Task<Credential?> GetByIdAsync(string tenantId, string id, CancellationToken ct = default)
    {
        try
        {
            var response = await _tableClient.GetEntityAsync<CredentialTableEntity>(tenantId, id, cancellationToken: ct);
            return response.Value.ToDomain();
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<Credential>> GetAllAsync(string tenantId, CancellationToken ct = default)
    {
        var credentials = new List<Credential>();
        var filter = $"PartitionKey eq '{tenantId}'";
        var query = _tableClient.QueryAsync<CredentialTableEntity>(filter: filter, cancellationToken: ct);

        await foreach (var entity in query)
        {
            credentials.Add(entity.ToDomain());
        }

        return credentials;
    }

    public async Task<IReadOnlyList<Credential>> GetByTypeAsync(string tenantId, CredentialType type, CancellationToken ct = default)
    {
        var credentials = new List<Credential>();
        var filter = $"PartitionKey eq '{tenantId}' and Type eq '{type}'";
        var query = _tableClient.QueryAsync<CredentialTableEntity>(filter: filter, cancellationToken: ct);

        await foreach (var entity in query)
        {
            credentials.Add(entity.ToDomain());
        }

        return credentials;
    }

    public async Task<IReadOnlyList<Credential>> GetExpiringAsync(string tenantId, int daysThreshold = 7, CancellationToken ct = default)
    {
        var credentials = new List<Credential>();
        var threshold = DateTimeOffset.UtcNow.AddDays(daysThreshold);
        var filter = $"PartitionKey eq '{tenantId}' and ExpiresAt le datetime'{threshold:O}' and ExpiresAt ge datetime'{DateTimeOffset.UtcNow:O}'";
        var query = _tableClient.QueryAsync<CredentialTableEntity>(filter: filter, cancellationToken: ct);

        await foreach (var entity in query)
        {
            credentials.Add(entity.ToDomain());
        }

        return credentials;
    }

    public async Task<IReadOnlyList<Credential>> GetExpiredAsync(string tenantId, CancellationToken ct = default)
    {
        var credentials = new List<Credential>();
        var now = DateTimeOffset.UtcNow;
        var filter = $"PartitionKey eq '{tenantId}' and ExpiresAt lt datetime'{now:O}'";
        var query = _tableClient.QueryAsync<CredentialTableEntity>(filter: filter, cancellationToken: ct);

        await foreach (var entity in query)
        {
            credentials.Add(entity.ToDomain());
        }

        return credentials;
    }

    public async Task<Credential?> GetByKeyVaultNameAsync(string tenantId, string keyVaultSecretName, CancellationToken ct = default)
    {
        var filter = $"PartitionKey eq '{tenantId}' and KeyVaultSecretName eq '{keyVaultSecretName}'";
        var query = _tableClient.QueryAsync<CredentialTableEntity>(filter: filter, maxPerPage: 1, cancellationToken: ct);

        await foreach (var entity in query)
        {
            return entity.ToDomain();
        }

        return null;
    }

    public async Task AddAsync(Credential credential, CancellationToken ct = default)
    {
        var entity = CredentialTableEntity.FromDomain(credential);
        await _tableClient.AddEntityAsync(entity, ct);
    }

    public async Task UpdateAsync(Credential credential, CancellationToken ct = default)
    {
        var entity = CredentialTableEntity.FromDomain(credential);
        await _tableClient.UpsertEntityAsync(entity, TableUpdateMode.Replace, ct);
    }

    public async Task DeleteAsync(string tenantId, string id, CancellationToken ct = default)
    {
        await _tableClient.DeleteEntityAsync(tenantId, id, cancellationToken: ct);
    }
}
