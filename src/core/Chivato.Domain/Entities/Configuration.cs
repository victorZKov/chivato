namespace Chivato.Domain.Entities;

/// <summary>
/// Tenant configuration settings
/// </summary>
public class Configuration : BaseEntity
{
    public string Key { get; private set; } = string.Empty;
    public string Value { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public ConfigurationCategory Category { get; private set; }

    private Configuration() { }

    public static Configuration Create(
        string tenantId,
        string key,
        string value,
        ConfigurationCategory category,
        string? description = null)
    {
        return new Configuration
        {
            Id = $"{tenantId}_{key}",
            TenantId = tenantId,
            Key = key,
            Value = value,
            Category = category,
            Description = description,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    public void UpdateValue(string newValue)
    {
        Value = newValue;
        UpdatedAt = DateTimeOffset.UtcNow;

        AddDomainEvent(new ConfigurationChangedEvent(TenantId, Key, newValue));
    }

    // Well-known configuration keys
    public static class Keys
    {
        public const string ScanIntervalHours = "scan_interval_hours";
        public const string EmailNotificationsEnabled = "email_notifications_enabled";
        public const string SlackWebhookUrl = "slack_webhook_url";
        public const string MinimumSeverityForAlert = "minimum_severity_for_alert";
        public const string AiModelName = "ai_model_name";
        public const string AiEndpoint = "ai_endpoint";
        public const string MaxConcurrentScans = "max_concurrent_scans";
        public const string RetentionDays = "retention_days";
    }

    // Default values
    public static class Defaults
    {
        public const string ScanIntervalHours = "24";
        public const string EmailNotificationsEnabled = "true";
        public const string MinimumSeverityForAlert = "High";
        public const string MaxConcurrentScans = "2";
        public const string RetentionDays = "90";
    }

    /// <summary>
    /// Reconstitute a Configuration from persistence
    /// </summary>
    public static Configuration Reconstitute(
        string id,
        string tenantId,
        string key,
        string value,
        ConfigurationCategory category,
        string? description,
        DateTimeOffset createdAt,
        DateTimeOffset? updatedAt)
    {
        return new Configuration
        {
            Id = id,
            TenantId = tenantId,
            Key = key,
            Value = value,
            Category = category,
            Description = description,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt
        };
    }
}

public enum ConfigurationCategory
{
    General,
    Scanning,
    Notifications,
    AI,
    Security
}

// Domain Events
public record ConfigurationChangedEvent(string TenantId, string Key, string NewValue) : IDomainEvent
{
    public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
}
