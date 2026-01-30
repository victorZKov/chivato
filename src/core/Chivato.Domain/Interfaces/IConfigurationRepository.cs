using Chivato.Domain.Entities;

namespace Chivato.Domain.Interfaces;

public interface IConfigurationRepository
{
    Task<Configuration?> GetAsync(string tenantId, string key, CancellationToken ct = default);
    Task<IReadOnlyList<Configuration>> GetAllAsync(string tenantId, CancellationToken ct = default);
    Task<IReadOnlyList<Configuration>> GetByCategoryAsync(string tenantId, ConfigurationCategory category, CancellationToken ct = default);
    Task<T> GetValueAsync<T>(string tenantId, string key, T defaultValue, CancellationToken ct = default);
    Task SetAsync(Configuration configuration, CancellationToken ct = default);
    Task DeleteAsync(string tenantId, string key, CancellationToken ct = default);

    // Convenience methods for common settings
    Task<int> GetScanIntervalHoursAsync(string tenantId, CancellationToken ct = default);
    Task<bool> GetEmailNotificationsEnabledAsync(string tenantId, CancellationToken ct = default);
}
