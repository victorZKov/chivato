using Azure;
using Azure.Data.Tables;

namespace Chivato.Functions.Models;

/// <summary>
/// Configuration settings stored in Azure Table
/// </summary>
public class ConfigurationEntity : ITableEntity
{
    public string PartitionKey { get; set; } = "config";
    public string RowKey { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }
}

/// <summary>
/// Azure connection configuration (secrets stored in Key Vault)
/// </summary>
public class AzureConnectionEntity : ITableEntity
{
    public string PartitionKey { get; set; } = "azure";
    public string RowKey { get; set; } = string.Empty; // GUID
    public string Name { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string KeyVaultSecretName { get; set; } = string.Empty;
    public string SubscriptionIds { get; set; } = string.Empty; // JSON array
    public string Status { get; set; } = "active";
    public DateTimeOffset? ExpiresAt { get; set; }
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }
}

/// <summary>
/// Azure DevOps connection configuration
/// </summary>
public class AdoConnectionEntity : ITableEntity
{
    public string PartitionKey { get; set; } = "ado";
    public string RowKey { get; set; } = string.Empty; // GUID
    public string Name { get; set; } = string.Empty;
    public string OrganizationUrl { get; set; } = string.Empty;
    public string AuthType { get; set; } = "PAT"; // PAT or OAuth
    public string KeyVaultSecretName { get; set; } = string.Empty;
    public string Status { get; set; } = "active";
    public DateTimeOffset? ExpiresAt { get; set; }
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }
}

/// <summary>
/// Pipeline configuration for monitoring
/// </summary>
public class PipelineEntity : ITableEntity
{
    public string PartitionKey { get; set; } = string.Empty; // Organization ID
    public string RowKey { get; set; } = string.Empty; // Pipeline ID
    public string OrganizationUrl { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public string PipelineName { get; set; } = string.Empty;
    public string PipelineId { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public string AdoConnectionId { get; set; } = string.Empty;
    public string AzureConnectionId { get; set; } = string.Empty;
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }
}

/// <summary>
/// Drift record detected during analysis
/// </summary>
public class DriftRecordEntity : ITableEntity
{
    public string PartitionKey { get; set; } = string.Empty; // Date: yyyyMMdd
    public string RowKey { get; set; } = string.Empty; // GUID
    public string PipelineId { get; set; } = string.Empty;
    public string PipelineName { get; set; } = string.Empty;
    public string ResourceId { get; set; } = string.Empty;
    public string ResourceType { get; set; } = string.Empty;
    public string ResourceName { get; set; } = string.Empty;
    public string Property { get; set; } = string.Empty;
    public string ExpectedValue { get; set; } = string.Empty;
    public string ActualValue { get; set; } = string.Empty;
    public string DriftType { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty; // Critical, High, Medium, Low, Info
    public string Description { get; set; } = string.Empty;
    public string Recommendation { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty; // security, performance, cost, compliance
    public DateTimeOffset DetectedAt { get; set; }
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }
}

/// <summary>
/// Email recipient for reports
/// </summary>
public class EmailRecipientEntity : ITableEntity
{
    public string PartitionKey { get; set; } = "recipients";
    public string RowKey { get; set; } = string.Empty; // GUID
    public string Email { get; set; } = string.Empty;
    public string NotifyOn { get; set; } = "always"; // always, drift_only, weekly
    public bool IsActive { get; set; } = true;
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }
}

/// <summary>
/// AI connection configuration
/// </summary>
public class AiConnectionEntity : ITableEntity
{
    public string PartitionKey { get; set; } = "ai";
    public string RowKey { get; set; } = string.Empty; // GUID
    public string Name { get; set; } = string.Empty;
    public string Endpoint { get; set; } = string.Empty;
    public string DeploymentName { get; set; } = string.Empty;
    public string AuthType { get; set; } = "ApiKey"; // ApiKey or ManagedIdentity
    public string KeyVaultSecretName { get; set; } = string.Empty;
    public string Status { get; set; } = "active";
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }
}

/// <summary>
/// Email service configuration
/// </summary>
public class EmailServiceConfigEntity : ITableEntity
{
    public string PartitionKey { get; set; } = "email";
    public string RowKey { get; set; } = "primary";
    public string KeyVaultSecretName { get; set; } = string.Empty;
    public string FromEmail { get; set; } = string.Empty;
    public string FromDisplayName { get; set; } = "Chivato Alerts";
    public bool IsActive { get; set; } = true;
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }
}
