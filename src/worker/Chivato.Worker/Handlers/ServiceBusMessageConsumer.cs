using Azure.Messaging.ServiceBus;
using Chivato.Shared.Models;
using Chivato.Shared.Models.Messages;
using Chivato.Shared.Services;
using Chivato.Worker.Processors;
using System.Text.Json;

namespace Chivato.Worker.Handlers;

/// <summary>
/// Service Bus message handler for drift analysis requests (Production)
/// </summary>
public class ServiceBusMessageConsumer : BackgroundService, IMessageConsumer
{
    private readonly ServiceBusClient _serviceBusClient;
    private readonly ServiceBusProcessor _processor;
    private readonly IDriftAnalysisProcessor _analysisProcessor;
    private readonly IStorageService _storageService;
    private readonly ISignalRService _signalRService;
    private readonly ILogger<ServiceBusMessageConsumer> _logger;

    private const string QueueName = "drift-analysis-requests";

    public ServiceBusMessageConsumer(
        string connectionString,
        IDriftAnalysisProcessor analysisProcessor,
        IStorageService storageService,
        ISignalRService signalRService,
        ILogger<ServiceBusMessageConsumer> logger)
    {
        _serviceBusClient = new ServiceBusClient(connectionString);
        _processor = _serviceBusClient.CreateProcessor(QueueName, new ServiceBusProcessorOptions
        {
            MaxConcurrentCalls = 2,
            AutoCompleteMessages = false,
            MaxAutoLockRenewalDuration = TimeSpan.FromMinutes(30)
        });

        _analysisProcessor = analysisProcessor;
        _storageService = storageService;
        _signalRService = signalRService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _processor.ProcessMessageAsync += ProcessMessageAsync;
        _processor.ProcessErrorAsync += ProcessErrorAsync;

        _logger.LogInformation("Starting Service Bus processor for queue: {QueueName}", QueueName);

        await _processor.StartProcessingAsync(stoppingToken);

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Stopping Service Bus processor");
        }

        await _processor.StopProcessingAsync(CancellationToken.None);
    }

    private async Task ProcessMessageAsync(ProcessMessageEventArgs args)
    {
        var messageBody = args.Message.Body.ToString();
        var correlationId = args.Message.CorrelationId ?? Guid.NewGuid().ToString();

        _logger.LogInformation("Processing message {MessageId} with correlation {CorrelationId}",
            args.Message.MessageId, correlationId);

        DriftAnalysisMessage? message = null;

        try
        {
            message = JsonSerializer.Deserialize<DriftAnalysisMessage>(messageBody,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (message == null)
            {
                _logger.LogWarning("Failed to deserialize message {MessageId}", args.Message.MessageId);
                await args.DeadLetterMessageAsync(args.Message, "InvalidMessage", "Could not deserialize message");
                return;
            }

            // Update status to processing
            await UpdateAnalysisStatusAsync(message.CorrelationId, "processing", message.TenantId);

            // Create progress reporter
            var progress = new Progress<AnalysisProgressEvent>(async evt =>
            {
                await _signalRService.SendAnalysisProgressAsync(evt.TenantId, evt);
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

            _logger.LogInformation("Successfully processed message {MessageId}. Status: {Status}, Drifts: {DriftCount}",
                args.Message.MessageId, result.Status, result.DriftItemCount);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Message processing cancelled for {MessageId}", args.Message.MessageId);
            // Don't complete or abandon - let the lock expire
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing message {MessageId}", args.Message.MessageId);

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
                await args.DeadLetterMessageAsync(args.Message, "MaxRetriesExceeded", ex.Message);
            }
            else
            {
                await args.AbandonMessageAsync(args.Message);
            }
        }
    }

    private Task ProcessErrorAsync(ProcessErrorEventArgs args)
    {
        _logger.LogError(args.Exception,
            "Service Bus error in entity {EntityPath}. Error source: {ErrorSource}",
            args.EntityPath, args.ErrorSource);
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

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        await _processor.DisposeAsync();
        await _serviceBusClient.DisposeAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _processor.DisposeAsync();
        await _serviceBusClient.DisposeAsync();
    }
}
