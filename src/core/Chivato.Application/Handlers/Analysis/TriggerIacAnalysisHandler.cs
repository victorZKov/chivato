using Chivato.Application.Commands.Analysis;
using Chivato.Application.Common;
using Chivato.Domain.Interfaces;
using Chivato.Shared.Models.Messages;
using MediatR;

namespace Chivato.Application.Handlers.Analysis;

/// <summary>
/// Handler for triggering IaC analysis (terraform plan)
/// </summary>
public class TriggerIacAnalysisHandler : IRequestHandler<TriggerIacAnalysisCommand, TriggerIacAnalysisResult>
{
    private readonly IMessageQueueService _messageQueue;
    private readonly IPipelineRepository _pipelineRepository;
    private readonly ICurrentUser _currentUser;

    private const string QueueName = "iac-analysis-requests";

    public TriggerIacAnalysisHandler(
        IMessageQueueService messageQueue,
        IPipelineRepository pipelineRepository,
        ICurrentUser currentUser)
    {
        _messageQueue = messageQueue;
        _pipelineRepository = pipelineRepository;
        _currentUser = currentUser;
    }

    public async Task<TriggerIacAnalysisResult> Handle(
        TriggerIacAnalysisCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Get pipeline details
            var pipeline = await _pipelineRepository.GetByIdAsync(
                _currentUser.TenantId,
                request.PipelineId,
                cancellationToken);

            if (pipeline == null)
            {
                return new TriggerIacAnalysisResult(
                    string.Empty,
                    false,
                    $"Pipeline {request.PipelineId} not found");
            }

            var correlationId = Guid.NewGuid().ToString();

            // Build the IaC analysis message
            var message = new IacAnalysisMessage
            {
                CorrelationId = correlationId,
                TriggerType = "AdHoc",
                PipelineId = pipeline.Id,
                TenantId = _currentUser.TenantId,
                IacType = request.IacType,
                InitiatedBy = _currentUser.UserId,
                Repository = new RepositoryInfo
                {
                    OrganizationUrl = $"https://dev.azure.com/{pipeline.Organization}",
                    ProjectName = pipeline.Project,
                    RepositoryName = pipeline.RepositoryId,
                    Branch = pipeline.Branch,
                    IacPath = pipeline.TerraformPath,
                    AdoConnectionId = pipeline.AdoConnectionId ?? ""
                },
                AzureCredentials = !string.IsNullOrEmpty(pipeline.AzureConnectionId)
                    ? new AzureCredentialsInfo
                    {
                        AzureConnectionId = pipeline.AzureConnectionId,
                        SubscriptionId = pipeline.SubscriptionId,
                        ResourceGroups = !string.IsNullOrEmpty(pipeline.ResourceGroup)
                            ? new List<string> { pipeline.ResourceGroup }
                            : new List<string>()
                    }
                    : null
            };

            // Send to IaC analysis queue
            await _messageQueue.SendAsync(QueueName, message, cancellationToken);

            return new TriggerIacAnalysisResult(correlationId, true);
        }
        catch (Exception ex)
        {
            return new TriggerIacAnalysisResult(string.Empty, false, ex.Message);
        }
    }
}
