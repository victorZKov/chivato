using Chivato.Domain.Entities;

namespace Chivato.Infrastructure.TableEntities;

public class ConfigurationTableEntity : BaseTableEntity
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Category { get; set; } = "General";

    public static ConfigurationTableEntity FromDomain(Configuration config)
    {
        return new ConfigurationTableEntity
        {
            PartitionKey = config.TenantId,
            RowKey = config.Key,
            Key = config.Key,
            Value = config.Value,
            Description = config.Description,
            Category = config.Category.ToString(),
            CreatedAt = config.CreatedAt,
            UpdatedAt = config.UpdatedAt
        };
    }

    public Configuration ToDomain()
    {
        return Configuration.Reconstitute(
            id: $"{PartitionKey}_{RowKey}",
            tenantId: PartitionKey,
            key: Key,
            value: Value,
            category: Enum.Parse<ConfigurationCategory>(Category),
            description: Description,
            createdAt: CreatedAt,
            updatedAt: UpdatedAt
        );
    }
}
