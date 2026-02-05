namespace Chivato.Shared.Models.Messages;

/// <summary>
/// Message envelope for IaC analysis requests (terraform plan, bicep, etc.)
/// </summary>
public class IacAnalysisMessage
{
    /// <summary>
    /// Unique correlation ID for tracking the request end-to-end
    /// </summary>
    public string CorrelationId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Type of analysis trigger: Scheduled, AdHoc, Retry
    /// </summary>
    public string TriggerType { get; set; } = "AdHoc";

    /// <summary>
    /// Pipeline ID to analyze
    /// </summary>
    public string PipelineId { get; set; } = string.Empty;

    /// <summary>
    /// Tenant ID for multi-tenant isolation
    /// </summary>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>
    /// IaC tool to use: terraform, bicep, ansible, pulumi
    /// </summary>
    public string IacType { get; set; } = "terraform";

    /// <summary>
    /// Repository information for cloning
    /// </summary>
    public RepositoryInfo Repository { get; set; } = new();

    /// <summary>
    /// Azure credentials for state access and resource querying
    /// </summary>
    public AzureCredentialsInfo? AzureCredentials { get; set; }

    /// <summary>
    /// User who initiated the request (for ad-hoc triggers)
    /// </summary>
    public string? InitiatedBy { get; set; }

    /// <summary>
    /// When the message was created
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Number of retry attempts
    /// </summary>
    public int RetryCount { get; set; } = 0;

    /// <summary>
    /// Priority: Normal, High (for ad-hoc requests)
    /// </summary>
    public string Priority { get; set; } = "Normal";
}

/// <summary>
/// Repository information for IaC analysis
/// </summary>
public class RepositoryInfo
{
    /// <summary>
    /// ADO organization URL (e.g., https://dev.azure.com/org)
    /// </summary>
    public string OrganizationUrl { get; set; } = string.Empty;

    /// <summary>
    /// ADO project name
    /// </summary>
    public string ProjectName { get; set; } = string.Empty;

    /// <summary>
    /// Repository name
    /// </summary>
    public string RepositoryName { get; set; } = string.Empty;

    /// <summary>
    /// Branch to analyze (default: main)
    /// </summary>
    public string Branch { get; set; } = "main";

    /// <summary>
    /// Path within the repo where IaC files are located (e.g., "terraform/", "infra/")
    /// </summary>
    public string IacPath { get; set; } = string.Empty;

    /// <summary>
    /// Connection ID for ADO credentials (references connection in storage)
    /// </summary>
    public string AdoConnectionId { get; set; } = string.Empty;
}

/// <summary>
/// Azure credentials reference for terraform state and provider auth
/// </summary>
public class AzureCredentialsInfo
{
    /// <summary>
    /// Connection ID for Azure credentials (references connection in storage)
    /// </summary>
    public string AzureConnectionId { get; set; } = string.Empty;

    /// <summary>
    /// Subscription ID for the resources
    /// </summary>
    public string SubscriptionId { get; set; } = string.Empty;

    /// <summary>
    /// Optional: specific resource groups to analyze
    /// </summary>
    public List<string> ResourceGroups { get; set; } = new();
}

/// <summary>
/// Result message for completed IaC analysis
/// </summary>
public class IacAnalysisResultMessage
{
    public string CorrelationId { get; set; } = string.Empty;
    public string Status { get; set; } = "Completed"; // Completed, Failed, PartialSuccess
    public string PipelineId { get; set; } = string.Empty;
    public string PipelineName { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public string IacType { get; set; } = string.Empty;
    public int DriftItemCount { get; set; }
    public string OverallRisk { get; set; } = "NONE";
    public DateTimeOffset CompletedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? ErrorMessage { get; set; }
    public TimeSpan ProcessingDuration { get; set; }

    /// <summary>
    /// Terraform plan output summary
    /// </summary>
    public TerraformPlanSummary? PlanSummary { get; set; }
}

/// <summary>
/// Summary from terraform plan output
/// </summary>
public class TerraformPlanSummary
{
    public int ToAdd { get; set; }
    public int ToChange { get; set; }
    public int ToDestroy { get; set; }
    public int ToImport { get; set; }
    public List<TerraformResourceChange> ResourceChanges { get; set; } = new();
}

/// <summary>
/// Individual resource change from terraform plan
/// </summary>
public class TerraformResourceChange
{
    public string Address { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ProviderName { get; set; } = string.Empty;
    public List<string> Actions { get; set; } = new(); // create, update, delete, no-op
    public Dictionary<string, PropertyChange> PropertyChanges { get; set; } = new();
}

/// <summary>
/// Property change detail
/// </summary>
public class PropertyChange
{
    public string? Before { get; set; }
    public string? After { get; set; }
    public bool IsSensitive { get; set; }
}

/// <summary>
/// IaC analysis progress event for SignalR
/// </summary>
public class IacProgressEvent
{
    public string Type { get; set; } = "iac_progress";
    public string CorrelationId { get; set; } = string.Empty;
    public string PipelineId { get; set; } = string.Empty;
    public string PipelineName { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public string IacType { get; set; } = string.Empty;
    public string Stage { get; set; } = string.Empty;
    public int Progress { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
}
