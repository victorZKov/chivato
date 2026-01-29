using Chivato.Shared.Models;
using Chivato.Shared.Models.Messages;

namespace Chivato.Worker.Processors;

/// <summary>
/// Interface for drift analysis processing
/// </summary>
public interface IDriftAnalysisProcessor
{
    /// <summary>
    /// Process a drift analysis request
    /// </summary>
    Task<DriftAnalysisResultMessage> ProcessAsync(
        DriftAnalysisMessage message,
        IProgress<AnalysisProgressEvent>? progress = null,
        CancellationToken cancellationToken = default);
}
