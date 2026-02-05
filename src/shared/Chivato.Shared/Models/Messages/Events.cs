namespace Chivato.Shared.Models.Messages;

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
    public string Status { get; set; } = string.Empty;
    public int DriftCount { get; set; }
    public string OverallRisk { get; set; } = string.Empty;
    public TimeSpan Duration { get; set; }
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
