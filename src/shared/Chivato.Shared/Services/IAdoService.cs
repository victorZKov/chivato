using Chivato.Shared.Models;

namespace Chivato.Shared.Services;

/// <summary>
/// Azure DevOps integration service
/// </summary>
public interface IAdoService
{
    /// <summary>
    /// Test connection to Azure DevOps
    /// </summary>
    Task<bool> TestConnectionAsync(string organizationUrl, string pat);

    /// <summary>
    /// Get list of projects in the organization
    /// </summary>
    Task<IEnumerable<AdoProject>> GetProjectsAsync(string organizationUrl, string pat);

    /// <summary>
    /// Get list of pipelines in a project
    /// </summary>
    Task<IEnumerable<AdoPipelineInfo>> GetPipelinesAsync(string organizationUrl, string pat, string projectName);

    /// <summary>
    /// Scan a pipeline for infrastructure definitions
    /// </summary>
    Task<PipelineScanResult> ScanPipelineAsync(
        string organizationUrl,
        string pat,
        string projectName,
        string pipelineId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Trigger a pipeline run with optional parameters
    /// </summary>
    Task<PipelineRunResult> TriggerPipelineRunAsync(
        string organizationUrl,
        string pat,
        string projectName,
        string pipelineId,
        Dictionary<string, string>? parameters = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get the status of a pipeline run
    /// </summary>
    Task<PipelineRunStatus> GetPipelineRunStatusAsync(
        string organizationUrl,
        string pat,
        string projectName,
        int runId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get the logs of a pipeline run
    /// </summary>
    Task<PipelineRunLogs> GetPipelineRunLogsAsync(
        string organizationUrl,
        string pat,
        string projectName,
        int runId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get the last pipeline run for a specific pipeline
    /// </summary>
    Task<PipelineRunInfo?> GetLastPipelineRunAsync(
        string organizationUrl,
        string pat,
        string projectName,
        string pipelineId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get the latest commit from a repository
    /// </summary>
    Task<GitCommitInfo?> GetLatestCommitAsync(
        string organizationUrl,
        string pat,
        string projectName,
        string repositoryName,
        string branch = "main",
        CancellationToken cancellationToken = default);
}

/// <summary>
/// ADO Project info
/// </summary>
public class AdoProject
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// ADO Pipeline info
/// </summary>
public class AdoPipelineInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Folder { get; set; } = string.Empty;
}

/// <summary>
/// Result of triggering a pipeline run
/// </summary>
public class PipelineRunResult
{
    public bool Success { get; set; }
    public int RunId { get; set; }
    public string RunUrl { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Status of a pipeline run
/// </summary>
public class PipelineRunStatus
{
    public int RunId { get; set; }
    public string State { get; set; } = string.Empty; // inProgress, completed, canceling
    public string Result { get; set; } = string.Empty; // succeeded, failed, canceled
    public string? SourceCommitId { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
}

/// <summary>
/// Logs from a pipeline run
/// </summary>
public class PipelineRunLogs
{
    public int RunId { get; set; }
    public List<PipelineLogEntry> Logs { get; set; } = new();
    public string FullLog { get; set; } = string.Empty;
}

/// <summary>
/// Individual log entry from a pipeline
/// </summary>
public class PipelineLogEntry
{
    public int LogId { get; set; }
    public string StageName { get; set; } = string.Empty;
    public string JobName { get; set; } = string.Empty;
    public string TaskName { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}

/// <summary>
/// Information about a pipeline run
/// </summary>
public class PipelineRunInfo
{
    public int RunId { get; set; }
    public string PipelineId { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string Result { get; set; } = string.Empty;
    public string? SourceCommitId { get; set; }
    public string? SourceBranch { get; set; }
    public DateTimeOffset? CreatedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
}

/// <summary>
/// Git commit information
/// </summary>
public class GitCommitInfo
{
    public string CommitId { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public DateTimeOffset CommittedAt { get; set; }
}
