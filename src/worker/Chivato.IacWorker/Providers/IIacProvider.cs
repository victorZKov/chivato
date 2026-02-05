using Chivato.Shared.Models.Messages;

namespace Chivato.IacWorker.Providers;

/// <summary>
/// Abstraction for Infrastructure as Code providers (Terraform, Bicep, Ansible, Pulumi, etc.)
/// </summary>
public interface IIacProvider
{
    /// <summary>
    /// Unique name for this provider (terraform, bicep, ansible, pulumi)
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Display name for UI
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Supported file extensions (e.g., .tf, .bicep, .yml)
    /// </summary>
    IReadOnlyList<string> SupportedExtensions { get; }

    /// <summary>
    /// Check if the provider tools are available in the environment
    /// </summary>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get the version of the installed tool
    /// </summary>
    Task<string?> GetVersionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Initialize the IaC working directory (e.g., terraform init)
    /// </summary>
    Task<IacInitResult> InitializeAsync(
        string workingDirectory,
        IacEnvironmentConfig config,
        IProgress<IacProgressEvent>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Analyze drift by comparing expected vs actual state
    /// </summary>
    Task<IacAnalysisResult> AnalyzeDriftAsync(
        string workingDirectory,
        IacEnvironmentConfig config,
        IProgress<IacProgressEvent>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validate IaC configuration without applying
    /// </summary>
    Task<IacValidationResult> ValidateAsync(
        string workingDirectory,
        IacEnvironmentConfig config,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Configuration for IaC environment
/// </summary>
public class IacEnvironmentConfig
{
    /// <summary>
    /// Environment variables to set (for cloud provider auth, etc.)
    /// </summary>
    public Dictionary<string, string> EnvironmentVariables { get; set; } = new();

    /// <summary>
    /// Backend configuration (for terraform backend, etc.)
    /// </summary>
    public Dictionary<string, string> BackendConfig { get; set; } = new();

    /// <summary>
    /// Variables to pass (terraform -var, etc.)
    /// </summary>
    public Dictionary<string, string> Variables { get; set; } = new();

    /// <summary>
    /// Variable files to use (terraform -var-file)
    /// </summary>
    public List<string> VariableFiles { get; set; } = new();

    /// <summary>
    /// Target specific resources (terraform -target)
    /// </summary>
    public List<string> Targets { get; set; } = new();
}

/// <summary>
/// Result of IaC initialization
/// </summary>
public class IacInitResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string Output { get; set; } = string.Empty;
    public TimeSpan Duration { get; set; }
}

/// <summary>
/// Result of drift analysis
/// </summary>
public class IacAnalysisResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string RawOutput { get; set; } = string.Empty;
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// Detected drifts/changes
    /// </summary>
    public List<IacDrift> Drifts { get; set; } = new();

    /// <summary>
    /// Summary counts
    /// </summary>
    public IacDriftSummary Summary { get; set; } = new();
}

/// <summary>
/// Individual drift detected
/// </summary>
public class IacDrift
{
    public string ResourceAddress { get; set; } = string.Empty;
    public string ResourceType { get; set; } = string.Empty;
    public string ResourceName { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty; // create, update, delete, replace
    public string Severity { get; set; } = "MEDIUM";
    public string Category { get; set; } = "configuration";
    public string Description { get; set; } = string.Empty;
    public string Recommendation { get; set; } = string.Empty;

    /// <summary>
    /// Property-level changes
    /// </summary>
    public List<IacPropertyChange> PropertyChanges { get; set; } = new();
}

/// <summary>
/// Property-level change detail
/// </summary>
public class IacPropertyChange
{
    public string PropertyPath { get; set; } = string.Empty;
    public string? ExpectedValue { get; set; }
    public string? ActualValue { get; set; }
    public bool IsSensitive { get; set; }
}

/// <summary>
/// Summary of drift analysis
/// </summary>
public class IacDriftSummary
{
    public int ToAdd { get; set; }
    public int ToChange { get; set; }
    public int ToDestroy { get; set; }
    public int ToReplace { get; set; }
    public int NoOp { get; set; }
    public int Critical { get; set; }
    public int High { get; set; }
    public int Medium { get; set; }
    public int Low { get; set; }
}

/// <summary>
/// Result of IaC validation
/// </summary>
public class IacValidationResult
{
    public bool IsValid { get; set; }
    public List<IacValidationError> Errors { get; set; } = new();
    public List<IacValidationWarning> Warnings { get; set; } = new();
}

/// <summary>
/// Validation error
/// </summary>
public class IacValidationError
{
    public string File { get; set; } = string.Empty;
    public int? Line { get; set; }
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Validation warning
/// </summary>
public class IacValidationWarning
{
    public string File { get; set; } = string.Empty;
    public int? Line { get; set; }
    public string Message { get; set; } = string.Empty;
}
