using Chivato.Functions.Models;

namespace Chivato.Functions.Services;

public interface IAiAnalyzerService
{
    Task<bool> TestConnectionAsync(string endpoint, string deploymentName, string apiKey);
    Task<DriftAnalysisResult> AnalyzeDriftAsync(
        PipelineScanResult pipelineResult,
        IEnumerable<AzureResourceState> currentResources,
        string endpoint,
        string deploymentName,
        string apiKey);
}
