using Azure;
using Azure.Data.Tables;

namespace Chivato.Infrastructure.TableEntities;

/// <summary>
/// Base class for all Azure Table Storage entities
/// PartitionKey = TenantId for multi-tenant isolation
/// RowKey = Entity Id
/// </summary>
public abstract class BaseTableEntity : ITableEntity
{
    public string PartitionKey { get; set; } = string.Empty;
    public string RowKey { get; set; } = string.Empty;
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }

    // Convenience properties
    public string TenantId => PartitionKey;
    public string Id => RowKey;
}
