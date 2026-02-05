using Chivato.Shared.Models.Messages;

namespace Chivato.IacWorker.Processors;

/// <summary>
/// Processor for IaC drift analysis
/// </summary>
public interface IIacAnalysisProcessor
{
    /// <summary>
    /// Process an IaC analysis request
    /// </summary>
    Task<IacAnalysisResultMessage> ProcessAsync(
        IacAnalysisMessage message,
        IProgress<IacProgressEvent>? progress = null,
        CancellationToken cancellationToken = default);
}
