using Chivato.Domain.Entities;
using Chivato.Domain.ValueObjects;

namespace Chivato.Domain.Events;

// ==========================================
// Drift Analysis Events
// ==========================================

public record DriftAnalysisStartedEvent(
    string TenantId,
    string CorrelationId,
    string? PipelineId,
    string TriggeredBy
) : IDomainEvent
{
    public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
}

public record DriftAnalysisCompletedEvent(
    string TenantId,
    string CorrelationId,
    int TotalDriftsFound,
    int CriticalCount,
    int HighCount,
    TimeSpan Duration
) : IDomainEvent
{
    public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
}

public record DriftAnalysisFailedEvent(
    string TenantId,
    string CorrelationId,
    string Error
) : IDomainEvent
{
    public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
}

public record DriftDetectedEvent(
    string TenantId,
    string DriftId,
    string PipelineId,
    Severity Severity,
    string ResourceName,
    string Property
) : IDomainEvent
{
    public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
}

public record DriftResolvedEvent(
    string TenantId,
    string DriftId,
    string ResolvedBy
) : IDomainEvent
{
    public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
}

// ==========================================
// Notification Events
// ==========================================

public record NotificationSentEvent(
    string TenantId,
    string RecipientEmail,
    string NotificationType,
    bool Success
) : IDomainEvent
{
    public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
}

public record AlertTriggeredEvent(
    string TenantId,
    string AlertType,
    Severity Severity,
    string Message
) : IDomainEvent
{
    public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
}
