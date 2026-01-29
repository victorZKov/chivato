namespace Chivato.Shared.Models;

/// <summary>
/// Result from AI drift analysis
/// </summary>
public class DriftAnalysisResult
{
    public string Summary { get; set; } = string.Empty;
    public List<DriftItem> DriftItems { get; set; } = new();
    public string OverallRisk { get; set; } = "NONE"; // CRITICAL, HIGH, MEDIUM, LOW, NONE
    public bool ActionRequired { get; set; }
}

/// <summary>
/// Individual drift item detected
/// </summary>
public class DriftItem
{
    public string ResourceId { get; set; } = string.Empty;
    public string ResourceType { get; set; } = string.Empty;
    public string ResourceName { get; set; } = string.Empty;
    public string Property { get; set; } = string.Empty;
    public string ExpectedValue { get; set; } = string.Empty;
    public string ActualValue { get; set; } = string.Empty;
    public string Severity { get; set; } = "INFO"; // CRITICAL, HIGH, MEDIUM, LOW, INFO
    public string Description { get; set; } = string.Empty;
    public string Recommendation { get; set; } = string.Empty;
    public string Category { get; set; } = "configuration"; // security, performance, cost, compliance, configuration
}

/// <summary>
/// Pipeline scan result
/// </summary>
public class PipelineScanResult
{
    public string PipelineId { get; set; } = string.Empty;
    public string PipelineName { get; set; } = string.Empty;
    public string YamlContent { get; set; } = string.Empty;
    public List<InfrastructureDefinition> InfrastructureDefinitions { get; set; } = new();
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Infrastructure definition extracted from pipeline
/// </summary>
public class InfrastructureDefinition
{
    public string Type { get; set; } = string.Empty; // ARM, Bicep, Terraform
    public string Content { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public List<ExpectedResource> Resources { get; set; } = new();
}

/// <summary>
/// Expected resource from IaC definition
/// </summary>
public class ExpectedResource
{
    public string Type { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Dictionary<string, object> Properties { get; set; } = new();
}

/// <summary>
/// Current Azure resource state
/// </summary>
public class AzureResourceState
{
    public string ResourceId { get; set; } = string.Empty;
    public string ResourceType { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ResourceGroup { get; set; } = string.Empty;
    public string SubscriptionId { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public Dictionary<string, object> Properties { get; set; } = new();
    public Dictionary<string, string> Tags { get; set; } = new();
}

/// <summary>
/// Credential status for monitoring expiration
/// </summary>
public class CredentialStatus
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // Azure, ADO, AI, Email
    public DateTimeOffset? ExpiresAt { get; set; }
    public int DaysUntilExpiration { get; set; }
    public string Status { get; set; } = "ok"; // ok, warning, danger, expired
    public DateTimeOffset? LastTestedAt { get; set; }
    public string? LastTestResult { get; set; }
}

/// <summary>
/// Scan step for tracking analysis progress
/// </summary>
public class ScanStep
{
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = "pending"; // pending, running, success, failed, skipped
    public int DurationMs { get; set; }
    public string? Message { get; set; }
}

/// <summary>
/// Drift statistics
/// </summary>
public class DriftStats
{
    public int Total { get; set; }
    public int Critical { get; set; }
    public int High { get; set; }
    public int Medium { get; set; }
    public int Low { get; set; }
    public DateTimeOffset? LastAnalysis { get; set; }
}

/// <summary>
/// Scan statistics
/// </summary>
public class ScanStats
{
    public int Total { get; set; }
    public int Success { get; set; }
    public int Failed { get; set; }
    public int AvgDurationSeconds { get; set; }
}
