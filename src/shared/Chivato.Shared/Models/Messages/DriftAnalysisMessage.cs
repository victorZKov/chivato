namespace Chivato.Shared.Models.Messages;

/// <summary>
/// Message envelope for drift analysis requests via Service Bus
/// </summary>
public class DriftAnalysisMessage
{
    /// <summary>
    /// Unique correlation ID for tracking the request end-to-end
    /// </summary>
    public string CorrelationId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Type of analysis trigger: Scheduled, AdHoc, Retry
    /// </summary>
    public string TriggerType { get; set; } = "Scheduled";

    /// <summary>
    /// Single pipeline to analyze (null = analyze all active pipelines)
    /// </summary>
    public string? PipelineId { get; set; }

    /// <summary>
    /// Organization ID for the pipeline
    /// </summary>
    public string? OrganizationId { get; set; }

    /// <summary>
    /// Tenant ID for multi-tenant isolation
    /// </summary>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>
    /// User who initiated the request (for ad-hoc triggers)
    /// </summary>
    public string? InitiatedBy { get; set; }

    /// <summary>
    /// When the message was created
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Number of retry attempts
    /// </summary>
    public int RetryCount { get; set; } = 0;

    /// <summary>
    /// Priority: Normal, High (for ad-hoc requests)
    /// </summary>
    public string Priority { get; set; } = "Normal";

    /// <summary>
    /// Send email notification upon completion
    /// </summary>
    public bool SendNotification { get; set; } = true;

    /// <summary>
    /// Callback URL for webhook notification (optional)
    /// </summary>
    public string? CallbackUrl { get; set; }
}

/// <summary>
/// Result message for completed analysis (for SignalR notification)
/// </summary>
public class DriftAnalysisResultMessage
{
    public string CorrelationId { get; set; } = string.Empty;
    public string Status { get; set; } = "Completed"; // Completed, Failed, PartialSuccess
    public string PipelineId { get; set; } = string.Empty;
    public string PipelineName { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public int DriftItemCount { get; set; }
    public string OverallRisk { get; set; } = "NONE";
    public DateTimeOffset CompletedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? ErrorMessage { get; set; }
    public TimeSpan ProcessingDuration { get; set; }
}

/// <summary>
/// Progress event for SignalR real-time updates
/// </summary>
public class AnalysisProgressEvent
{
    public string Type { get; set; } = "analysis_progress";
    public string CorrelationId { get; set; } = string.Empty;
    public string PipelineId { get; set; } = string.Empty;
    public string PipelineName { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public string Stage { get; set; } = string.Empty;
    public int Progress { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Analysis completed event for SignalR
/// </summary>
public class AnalysisCompletedEvent
{
    public string Type { get; set; } = "analysis_completed";
    public string CorrelationId { get; set; } = string.Empty;
    public string PipelineId { get; set; } = string.Empty;
    public string PipelineName { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public AnalysisSummary Summary { get; set; } = new();
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Analysis failed event for SignalR
/// </summary>
public class AnalysisFailedEvent
{
    public string Type { get; set; } = "analysis_failed";
    public string CorrelationId { get; set; } = string.Empty;
    public string PipelineId { get; set; } = string.Empty;
    public string PipelineName { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public string Error { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Summary included in completed event
/// </summary>
public class AnalysisSummary
{
    public int TotalDrifts { get; set; }
    public int Critical { get; set; }
    public int High { get; set; }
    public int Medium { get; set; }
    public int Low { get; set; }
    public int DurationSeconds { get; set; }
}
