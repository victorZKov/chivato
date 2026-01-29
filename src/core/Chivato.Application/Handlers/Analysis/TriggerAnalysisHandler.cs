using Chivato.Application.Commands.Analysis;
using Chivato.Application.Common;
using MediatR;

namespace Chivato.Application.Handlers.Analysis;

public class TriggerAnalysisHandler : IRequestHandler<TriggerAnalysisCommand, TriggerAnalysisResult>
{
    private readonly IMessageQueueService _messageQueue;
    private readonly ICurrentUser _currentUser;

    private const string QueueName = "drift-analysis-requests";

    public TriggerAnalysisHandler(IMessageQueueService messageQueue, ICurrentUser currentUser)
    {
        _messageQueue = messageQueue;
        _currentUser = currentUser;
    }

    public async Task<TriggerAnalysisResult> Handle(TriggerAnalysisCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var correlationId = Guid.NewGuid().ToString();

            var message = new DriftAnalysisMessage(
                correlationId,
                _currentUser.TenantId,
                request.PipelineId,
                request.AnalyzeAll,
                _currentUser.UserId,
                DateTimeOffset.UtcNow
            );

            await _messageQueue.SendAsync(QueueName, message, cancellationToken);

            return new TriggerAnalysisResult(correlationId, true);
        }
        catch (Exception ex)
        {
            return new TriggerAnalysisResult(string.Empty, false, ex.Message);
        }
    }
}
