using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using Chivato.IacWorker.Processors;
using Chivato.Shared.Constants;
using Chivato.Shared.Models;
using Chivato.Shared.Models.Messages;
using Chivato.Shared.Services;
using System.Text.Json;

namespace Chivato.IacWorker.Handlers;

/// <summary>
/// Message consumer using Azure Storage Queues (works with Azurite for local development)
/// </summary>
public class StorageQueueIacConsumer : BackgroundService, IIacMessageConsumer
{
    private readonly QueueClient _queueClient;
    private readonly IIacAnalysisProcessor _processor;
    private readonly IStorageService _storageService;
    private readonly ISignalRService _signalRService;
    private readonly ILogger<StorageQueueIacConsumer> _logger;

    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan VisibilityTimeout = TimeSpan.FromMinutes(60); // IaC analysis can take longer

    public StorageQueueIacConsumer(
        string connectionString,
        IIacAnalysisProcessor processor,
        IStorageService storageService,
        ISignalRService signalRService,
        ILogger<StorageQueueIacConsumer> logger)
    {
        _queueClient = new QueueClient(connectionString, QueueNames.IacAnalysisRequests, new QueueClientOptions
        {
            MessageEncoding = QueueMessageEncoding.Base64
        });
        _processor = processor;
        _storageService = storageService;
        _signalRService = signalRService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting Storage Queue IaC consumer for queue: {QueueName}", QueueNames.IacAnalysisRequests);

        // Ensure queue exists
        await _queueClient.CreateIfNotExistsAsync(cancellationToken: stoppingToken);
        _logger.LogInformation("Queue {QueueName} ready", QueueNames.IacAnalysisRequests);

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
                _logger.LogError(ex, "Error polling IaC queue");
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }

        _logger.LogInformation("Storage Queue IaC consumer stopped");
    }

    private async Task ProcessMessageAsync(QueueMessage queueMessage, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing IaC message {MessageId}", queueMessage.MessageId);

        IacAnalysisMessage? message = null;

        try
        {
            var messageBody = queueMessage.Body.ToString();
            message = JsonSerializer.Deserialize<IacAnalysisMessage>(messageBody,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (message == null)
            {
                _logger.LogWarning("Failed to deserialize IaC message {MessageId}", queueMessage.MessageId);
                await _queueClient.DeleteMessageAsync(queueMessage.MessageId, queueMessage.PopReceipt, cancellationToken);
                return;
            }

            // Update status to processing
            await UpdateAnalysisStatusAsync(message.CorrelationId, "processing", message.TenantId);

            // Create progress reporter
            var progress = new Progress<IacProgressEvent>(async evt =>
            {
                await _signalRService.SendToTenantAsync(evt.TenantId, "iacProgress", evt);
            });

            // Process the analysis
            var result = await _processor.ProcessAsync(message, progress, cancellationToken);

            // Update final status
            await UpdateAnalysisStatusAsync(
                message.CorrelationId,
                result.Status.ToLowerInvariant(),
                message.TenantId,
                result.DriftItemCount,
                result.OverallRisk);

            // Note: SignalR notifications are sent by the processor, no need to duplicate here

            // Delete the processed message
            await _queueClient.DeleteMessageAsync(queueMessage.MessageId, queueMessage.PopReceipt, cancellationToken);

            _logger.LogInformation("Successfully processed IaC message {MessageId}. Status: {Status}, Drifts: {DriftCount}",
                queueMessage.MessageId, result.Status, result.DriftItemCount);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("IaC message processing cancelled for {MessageId}", queueMessage.MessageId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing IaC message {MessageId}", queueMessage.MessageId);

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
                _logger.LogWarning("IaC message {MessageId} exceeded max retries, deleting", queueMessage.MessageId);
                await _queueClient.DeleteMessageAsync(queueMessage.MessageId, queueMessage.PopReceipt, cancellationToken);
            }
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

    private string DetermineChangeSeverity(TerraformResourceChange change)
    {
        var action = change.Actions.FirstOrDefault() ?? "";

        if (action == "delete" || change.Actions.Contains("delete"))
            return "HIGH";

        var securityTypes = new[] { "firewall", "security", "policy", "role", "identity" };
        if (securityTypes.Any(t => change.Type.Contains(t, StringComparison.OrdinalIgnoreCase)))
            return "CRITICAL";

        return "MEDIUM";
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}
