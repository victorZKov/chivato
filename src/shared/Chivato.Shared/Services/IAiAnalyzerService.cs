using Chivato.Shared.Models;

namespace Chivato.Shared.Services;

/// <summary>
/// Abstraction for AI-powered drift analysis.
/// Allows switching between Azure OpenAI (enterprise) and Mistral/Scaleway (SaaS).
/// </summary>
public interface IAiAnalyzerService
{
    /// <summary>
    /// Test the AI connection
    /// </summary>
    Task<bool> TestConnectionAsync();

    /// <summary>
    /// Analyze drift between expected and actual infrastructure state
    /// </summary>
    Task<DriftAnalysisResult> AnalyzeDriftAsync(
        PipelineScanResult expectedState,
        IEnumerable<AzureResourceState> actualState,
        CancellationToken cancellationToken = default);
}
