using Azure.Messaging.ServiceBus;
using Chivato.IacWorker.Processors;
using Chivato.Shared.Constants;
using Chivato.Shared.Models;
using Chivato.Shared.Models.Messages;
using Chivato.Shared.Services;
using System.Text.Json;

namespace Chivato.IacWorker.Handlers;

/// <summary>
/// Message consumer using Azure Service Bus (for production)
/// </summary>
public class ServiceBusIacConsumer : BackgroundService, IIacMessageConsumer
{
    private readonly ServiceBusClient _client;
    private readonly ServiceBusProcessor _processor;
    private readonly IIacAnalysisProcessor _analysisProcessor;
    private readonly IStorageService _storageService;
    private readonly ISignalRService _signalRService;
    private readonly ILogger<ServiceBusIacConsumer> _logger;

    public ServiceBusIacConsumer(
        string connectionString,
        IIacAnalysisProcessor analysisProcessor,
        IStorageService storageService,
        ISignalRService signalRService,
        ILogger<ServiceBusIacConsumer> logger)
    {
        _client = new ServiceBusClient(connectionString);
        _processor = _client.CreateProcessor(QueueNames.IacAnalysisRequests, new ServiceBusProcessorOptions
        {
            MaxConcurrentCalls = 1, // Process one at a time due to resource intensity
            AutoCompleteMessages = false,
            MaxAutoLockRenewalDuration = TimeSpan.FromMinutes(60)
        });
        _analysisProcessor = analysisProcessor;
        _storageService = storageService;
        _signalRService = signalRService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting Service Bus IaC consumer for queue: {QueueName}", QueueNames.IacAnalysisRequests);

        _processor.ProcessMessageAsync += ProcessMessageAsync;
        _processor.ProcessErrorAsync += ProcessErrorAsync;

        await _processor.StartProcessingAsync(stoppingToken);

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Expected during shutdown
        }

        _logger.LogInformation("Service Bus IaC consumer stopping...");
        await _processor.StopProcessingAsync();
        _logger.LogInformation("Service Bus IaC consumer stopped");
    }

    private async Task ProcessMessageAsync(ProcessMessageEventArgs args)
    {
        var messageId = args.Message.MessageId;
        _logger.LogInformation("Processing IaC message {MessageId}", messageId);

        IacAnalysisMessage? message = null;

        try
        {
            var messageBody = args.Message.Body.ToString();
            message = JsonSerializer.Deserialize<IacAnalysisMessage>(messageBody,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (message == null)
            {
                _logger.LogWarning("Failed to deserialize IaC message {MessageId}", messageId);
                await args.CompleteMessageAsync(args.Message);
                return;
            }

            // Update status to processing
            await UpdateAnalysisStatusAsync(message.CorrelationId, "processing", message.TenantId);

            // Create progress reporter with SignalR
            var progress = new Progress<IacProgressEvent>(async evt =>
            {
                await _signalRService.SendToTenantAsync(evt.TenantId, "iacProgress", evt);
            });

            // Process the analysis
            var result = await _analysisProcessor.ProcessAsync(message, progress, args.CancellationToken);

            // Update final status
            await UpdateAnalysisStatusAsync(
                message.CorrelationId,
                result.Status.ToLowerInvariant(),
                message.TenantId,
                result.DriftItemCount,
                result.OverallRisk);

            // Send completion notification via SignalR
            if (result.Status == "Completed")
            {
                await _signalRService.SendAnalysisCompletedAsync(message.TenantId, new AnalysisCompletedEvent
                {
                    CorrelationId = message.CorrelationId,
                    PipelineId = result.PipelineId,
                    PipelineName = result.PipelineName,
                    TenantId = message.TenantId,
                    Summary = new AnalysisSummary
                    {
                        TotalDrifts = result.DriftItemCount,
                        DurationSeconds = (int)result.ProcessingDuration.TotalSeconds
                    }
                });
            }
            else if (result.Status == "Failed")
            {
                await _signalRService.SendAnalysisFailedAsync(message.TenantId, new AnalysisFailedEvent
                {
                    CorrelationId = message.CorrelationId,
                    PipelineId = result.PipelineId,
                    PipelineName = result.PipelineName,
                    TenantId = message.TenantId,
                    Error = result.ErrorMessage ?? "Unknown error"
                });
            }

            // Complete the message
            await args.CompleteMessageAsync(args.Message);

            _logger.LogInformation("Successfully processed IaC message {MessageId}. Status: {Status}, Drifts: {DriftCount}",
                messageId, result.Status, result.DriftItemCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing IaC message {MessageId}", messageId);

            // Update status to failed
            if (message != null)
            {
                await UpdateAnalysisStatusAsync(message.CorrelationId, "failed", message.TenantId, error: ex.Message);

                await _signalRService.SendAnalysisFailedAsync(message.TenantId, new AnalysisFailedEvent
                {
                    CorrelationId = message.CorrelationId,
                    TenantId = message.TenantId,
                    Error = ex.Message
                });
            }

            // Check delivery count for dead-letter
            if (args.Message.DeliveryCount >= 3)
            {
                _logger.LogWarning("IaC message {MessageId} exceeded max retries, dead-lettering", messageId);
                await args.DeadLetterMessageAsync(args.Message, "MaxRetriesExceeded", ex.Message);
            }
            else
            {
                // Abandon for retry
                await args.AbandonMessageAsync(args.Message);
            }
        }
    }

    private Task ProcessErrorAsync(ProcessErrorEventArgs args)
    {
        _logger.LogError(args.Exception, "Service Bus IaC processor error: {ErrorSource}", args.ErrorSource);
        return Task.CompletedTask;
    }

    private async Task UpdateAnalysisStatusAsync(
        string correlationId,
        string status,
        string tenantId,
        int driftCount = 0,
        string? overallRisk = null,
        string? error = null)
    {
        var analysisStatus = await _storageService.GetAnalysisStatusAsync(correlationId);

        if (analysisStatus == null)
        {
            analysisStatus = new AnalysisStatusEntity
            {
                PartitionKey = tenantId,
                RowKey = correlationId,
                Status = status,
                CreatedAt = DateTimeOffset.UtcNow
            };
        }

        analysisStatus.Status = status;

        if (status == "processing")
        {
            analysisStatus.StartedAt = DateTimeOffset.UtcNow;
        }
        else if (status == "completed" || status == "failed")
        {
            analysisStatus.CompletedAt = DateTimeOffset.UtcNow;
            analysisStatus.DriftCount = driftCount;
            analysisStatus.OverallRisk = overallRisk;
            analysisStatus.ErrorMessage = error;
        }

        await _storageService.SaveAnalysisStatusAsync(analysisStatus);
    }

    public async ValueTask DisposeAsync()
    {
        await _processor.DisposeAsync();
        await _client.DisposeAsync();
    }
}
