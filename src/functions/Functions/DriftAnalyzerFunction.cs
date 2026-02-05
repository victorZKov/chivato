using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Chivato.Functions.Services;
using Chivato.Shared.Services;
using Chivato.Shared.Models.Messages;

namespace Chivato.Functions.Functions;

public class DriftAnalyzerFunction
{
    private readonly ILogger<DriftAnalyzerFunction> _logger;
    private readonly IStorageService _storageService;
    private readonly Chivato.Shared.Services.IMessageQueueService _messageQueueService;

    public DriftAnalyzerFunction(
        ILogger<DriftAnalyzerFunction> logger,
        IStorageService storageService,
        Chivato.Shared.Services.IMessageQueueService messageQueueService)
    {
        _logger = logger;
        _storageService = storageService;
        _messageQueueService = messageQueueService;
    }

    /// <summary>
    /// Timer trigger that schedules drift analysis for configured pipelines
    /// Default: Every 24 hours at midnight UTC
    /// </summary>
    [Function("DriftAnalyzer")]
    public async Task Run([TimerTrigger("0 0 0 * * *")] TimerInfo timerInfo)
    {
        _logger.LogInformation("Drift Analyzer Scheduler started at: {time}", DateTime.UtcNow);

        try
        {
            // 1. Get active pipelines
            var pipelines = await _storageService.GetActivePipelinesAsync();
            _logger.LogInformation("Found {count} active pipelines to schedule analysis for", pipelines.Count());

            foreach (var pipeline in pipelines)
            {
                try
                {
                    _logger.LogInformation("Scheduling analysis for pipeline: {name}", pipeline.PipelineName);

                    var message = new IacAnalysisMessage
                    {
                        CorrelationId = Guid.NewGuid().ToString(),
                        TenantId = pipeline.TenantId,
                        PipelineId = pipeline.PipelineId,
                        TriggerType = "Scheduled",
                        IacType = "terraform"
                    };

                    await _messageQueueService.SendAnalysisMessageAsync(message);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error scheduling pipeline {id}", pipeline.PipelineId);
                }
            }

            _logger.LogInformation("Drift Analyzer Scheduler completed at: {time}", DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Drift Analyzer Scheduler failed");
            throw;
        }
    }
}
