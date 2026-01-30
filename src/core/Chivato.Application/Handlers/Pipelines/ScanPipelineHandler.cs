using Chivato.Application.Commands.Analysis;
using Chivato.Application.Commands.Pipelines;
using Chivato.Application.Common;
using Chivato.Domain.Interfaces;
using MediatR;

namespace Chivato.Application.Handlers.Pipelines;

public class ScanPipelineHandler : IRequestHandler<ScanPipelineCommand, ScanPipelineResult>
{
    private readonly IPipelineRepository _repository;
    private readonly IMessageQueueService _messageQueue;
    private readonly ICurrentUser _currentUser;

    private const string QueueName = "drift-analysis-requests";

    public ScanPipelineHandler(
        IPipelineRepository repository,
        IMessageQueueService messageQueue,
        ICurrentUser currentUser)
    {
        _repository = repository;
        _messageQueue = messageQueue;
        _currentUser = currentUser;
    }

    public async Task<ScanPipelineResult> Handle(ScanPipelineCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // Verify pipeline exists
            var pipeline = await _repository.GetByIdAsync(_currentUser.TenantId, request.PipelineId, cancellationToken);

            if (pipeline == null)
                return new ScanPipelineResult(string.Empty, false, "Pipeline not found");

            // Generate correlation ID
            var correlationId = Guid.NewGuid().ToString();

            // Queue analysis message
            var message = new DriftAnalysisMessage(
                correlationId,
                _currentUser.TenantId,
                request.PipelineId,
                AnalyzeAll: false,
                _currentUser.UserId,
                DateTimeOffset.UtcNow
            );

            await _messageQueue.SendAsync(QueueName, message, cancellationToken);

            return new ScanPipelineResult(correlationId, true);
        }
        catch (Exception ex)
        {
            return new ScanPipelineResult(string.Empty, false, ex.Message);
        }
    }
}
