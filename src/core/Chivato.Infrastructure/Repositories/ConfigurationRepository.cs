using Azure.Data.Tables;
using Chivato.Domain.Entities;
using Chivato.Domain.Interfaces;
using Chivato.Infrastructure.TableEntities;

namespace Chivato.Infrastructure.Repositories;

public class ConfigurationRepository : IConfigurationRepository
{
    private readonly TableClient _tableClient;
    private const string TableName = "Configurations";

    public ConfigurationRepository(TableServiceClient tableServiceClient)
    {
        _tableClient = tableServiceClient.GetTableClient(TableName);
        _tableClient.CreateIfNotExists();
    }

    public async Task<Configuration?> GetAsync(string tenantId, string key, CancellationToken ct = default)
    {
        try
        {
            var response = await _tableClient.GetEntityAsync<ConfigurationTableEntity>(tenantId, key, cancellationToken: ct);
            return response.Value.ToDomain();
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<Configuration>> GetAllAsync(string tenantId, CancellationToken ct = default)
    {
        var configs = new List<Configuration>();
        var filter = $"PartitionKey eq '{tenantId}'";
        var query = _tableClient.QueryAsync<ConfigurationTableEntity>(filter: filter, cancellationToken: ct);

        await foreach (var entity in query)
        {
            configs.Add(entity.ToDomain());
        }

        return configs;
    }

    public async Task<IReadOnlyList<Configuration>> GetByCategoryAsync(string tenantId, ConfigurationCategory category, CancellationToken ct = default)
    {
        var configs = new List<Configuration>();
        var filter = $"PartitionKey eq '{tenantId}' and Category eq '{category}'";
        var query = _tableClient.QueryAsync<ConfigurationTableEntity>(filter: filter, cancellationToken: ct);

        await foreach (var entity in query)
        {
            configs.Add(entity.ToDomain());
        }

        return configs;
    }

    public async Task<T> GetValueAsync<T>(string tenantId, string key, T defaultValue, CancellationToken ct = default)
    {
        var config = await GetAsync(tenantId, key, ct);
        if (config == null)
            return defaultValue;

        try
        {
            return (T)Convert.ChangeType(config.Value, typeof(T));
        }
        catch
        {
            return defaultValue;
        }
    }

    public async Task SetAsync(Configuration configuration, CancellationToken ct = default)
    {
        var entity = ConfigurationTableEntity.FromDomain(configuration);
        await _tableClient.UpsertEntityAsync(entity, TableUpdateMode.Replace, ct);
    }

    public async Task DeleteAsync(string tenantId, string key, CancellationToken ct = default)
    {
        await _tableClient.DeleteEntityAsync(tenantId, key, cancellationToken: ct);
    }

    public async Task<int> GetScanIntervalHoursAsync(string tenantId, CancellationToken ct = default)
    {
        return await GetValueAsync(tenantId, Configuration.Keys.ScanIntervalHours,
            int.Parse(Configuration.Defaults.ScanIntervalHours), ct);
    }

    public async Task<bool> GetEmailNotificationsEnabledAsync(string tenantId, CancellationToken ct = default)
    {
        return await GetValueAsync(tenantId, Configuration.Keys.EmailNotificationsEnabled,
            bool.Parse(Configuration.Defaults.EmailNotificationsEnabled), ct);
    }
}
