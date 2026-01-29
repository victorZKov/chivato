using Chivato.Functions.Models;

namespace Chivato.Functions.Services;

public interface IAdoService
{
    Task<bool> TestConnectionAsync(string organizationUrl, string pat);
    Task<IEnumerable<string>> GetProjectsAsync(string organizationUrl, string pat);
    Task<IEnumerable<(string Id, string Name)>> GetPipelinesAsync(string organizationUrl, string projectName, string pat);
    Task<PipelineScanResult> ScanPipelineAsync(string organizationUrl, string projectName, string pipelineId, string pat);
}
