namespace Chivato.Application.DTOs;

public record PipelineDto(
    string Id,
    string Name,
    string Organization,
    string Project,
    string RepositoryId,
    string Branch,
    string TerraformPath,
    string SubscriptionId,
    string ResourceGroup,
    string Status,
    DateTimeOffset? LastScanAt,
    int DriftCount,
    DateTimeOffset CreatedAt
);

public record PipelineDetailDto(
    string Id,
    string Name,
    string Organization,
    string Project,
    string RepositoryId,
    string Branch,
    string TerraformPath,
    string SubscriptionId,
    string ResourceGroup,
    string Status,
    DateTimeOffset? LastScanAt,
    int DriftCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    IReadOnlyList<DriftRecordDto>? RecentDrifts,
    IReadOnlyList<ScanLogDto>? RecentScans
);

public record DriftRecordDto(
    string Id,
    string PipelineId,
    string PipelineName,
    string Severity,
    string ResourceId,
    string ResourceType,
    string ResourceName,
    string Property,
    string ExpectedValue,
    string ActualValue,
    string Description,
    string Recommendation,
    string Category,
    DateTimeOffset DetectedAt,
    string Status
);

public record ScanLogDto(
    string Id,
    string PipelineId,
    string PipelineName,
    string CorrelationId,
    string Status,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    int DriftCount,
    int ResourcesScanned,
    int DurationSeconds,
    string TriggeredBy,
    string? ErrorMessage
);

public record DriftStatsDto(
    int Total,
    int Critical,
    int High,
    int Medium,
    int Low,
    DateTimeOffset? LastAnalysis
);

public record ScanStatsDto(
    int Total,
    int Success,
    int Failed,
    double AvgDurationSeconds
);

public record PagedResultDto<T>(
    IReadOnlyList<T> Items,
    int Total,
    int Page,
    int PageSize
);
