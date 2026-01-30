using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using Chivato.Shared.Models;
using Chivato.Shared.Models.Messages;
using Chivato.Shared.Services;
using Chivato.Worker.Processors;
using System.Text;
using System.Text.Json;

namespace Chivato.Worker.Handlers;

/// <summary>
/// Message consumer using Azure Storage Queues (works with Azurite for local development)
/// </summary>
public class StorageQueueConsumer : BackgroundService, IMessageConsumer
{
    private readonly QueueClient _queueClient;
    private readonly IDriftAnalysisProcessor _analysisProcessor;
    private readonly IStorageService _storageService;
    private readonly ISignalRService _signalRService;
    private readonly ILogger<StorageQueueConsumer> _logger;

    private const string QueueName = "drift-analysis-requests";
    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan VisibilityTimeout = TimeSpan.FromMinutes(30);

    public StorageQueueConsumer(
        string connectionString,
        IDriftAnalysisProcessor analysisProcessor,
        IStorageService storageService,
        ISignalRService signalRService,
        ILogger<StorageQueueConsumer> logger)
    {
        _queueClient = new QueueClient(connectionString, QueueName, new QueueClientOptions
        {
            MessageEncoding = QueueMessageEncoding.Base64
        });
        _analysisProcessor = analysisProcessor;
        _storageService = storageService;
        _signalRService = signalRService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting Storage Queue consumer for queue: {QueueName}", QueueName);

        // Ensure queue exists
        await _queueClient.CreateIfNotExistsAsync(cancellationToken: stoppingToken);
        _logger.LogInformation("Queue {QueueName} ready", QueueName);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                QueueMessage[] messages = await _queueClient.ReceiveMessagesAsync(
                    maxMessages: 1,
                    visibilityTimeout: VisibilityTimeout,
                    cancellationToken: stoppingToken);

                if (messages.Length > 0)
                {
                    await ProcessMessageAsync(messages[0], stoppingToken);
                }
                else
                {
                    await Task.Delay(PollingInterval, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error polling queue");
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }

        _logger.LogInformation("Storage Queue consumer stopped");
    }

    private async Task ProcessMessageAsync(QueueMessage queueMessage, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing message {MessageId}", queueMessage.MessageId);

        DriftAnalysisMessage? message = null;

        try
        {
            var messageBody = queueMessage.Body.ToString();
            message = JsonSerializer.Deserialize<DriftAnalysisMessage>(messageBody,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (message == null)
            {
                _logger.LogWarning("Failed to deserialize message {MessageId}", queueMessage.MessageId);
                await _queueClient.DeleteMessageAsync(queueMessage.MessageId, queueMessage.PopReceipt, cancellationToken);
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
            var result = await _analysisProcessor.ProcessAsync(message, progress, cancellationToken);

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

            // Delete the processed message
            await _queueClient.DeleteMessageAsync(queueMessage.MessageId, queueMessage.PopReceipt, cancellationToken);

            _logger.LogInformation("Successfully processed message {MessageId}. Status: {Status}, Drifts: {DriftCount}",
                queueMessage.MessageId, result.Status, result.DriftItemCount);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Message processing cancelled for {MessageId}", queueMessage.MessageId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing message {MessageId}", queueMessage.MessageId);

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

            // Check dequeue count for dead-letter equivalent
            if (queueMessage.DequeueCount >= 3)
            {
                _logger.LogWarning("Message {MessageId} exceeded max retries, deleting", queueMessage.MessageId);
                await _queueClient.DeleteMessageAsync(queueMessage.MessageId, queueMessage.PopReceipt, cancellationToken);
            }
            // Otherwise message becomes visible again after visibility timeout
        }
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

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}
