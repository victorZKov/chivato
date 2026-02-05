using Chivato.Application.Commands.Analysis;
using Chivato.Application.Common;
using Chivato.Domain.Interfaces;
using Chivato.Shared.Models.Messages;
using MediatR;

namespace Chivato.Application.Handlers.Analysis;

public class TriggerAnalysisHandler : IRequestHandler<TriggerAnalysisCommand, TriggerAnalysisResult>
{
    private readonly IMessageQueueService _messageQueue;
    private readonly ICurrentUser _currentUser;

    // Use IaC analysis queue for terraform plan analysis
    private const string QueueName = "iac-analysis-requests";

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

            // Send IaC analysis message for terraform plan-based drift detection
            var message = new IacAnalysisMessage
            {
                CorrelationId = correlationId,
                TenantId = _currentUser.TenantId,
                PipelineId = request.PipelineId ?? string.Empty,
                TriggerType = request.AnalyzeAll ? "ScheduledBatch" : "AdHoc",
                IacType = "terraform",
                InitiatedBy = _currentUser.UserId,
                CreatedAt = DateTimeOffset.UtcNow
            };

            await _messageQueue.SendAsync(QueueName, message, cancellationToken);

            return new TriggerAnalysisResult(correlationId, true);
        }
        catch (Exception ex)
        {
            return new TriggerAnalysisResult(string.Empty, false, ex.Message);
        }
    }
}
