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
