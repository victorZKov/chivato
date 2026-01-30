using Chivato.Application.DTOs;
using MediatR;

namespace Chivato.Application.Queries.Configuration;

/// <summary>
/// Get all configuration settings for the tenant
/// </summary>
public record GetConfigurationQuery() : IRequest<ConfigurationDto>;

/// <summary>
/// Get all Azure connections
/// </summary>
public record GetAzureConnectionsQuery() : IRequest<IReadOnlyList<AzureConnectionDto>>;

/// <summary>
/// Get all ADO connections
/// </summary>
public record GetAdoConnectionsQuery() : IRequest<IReadOnlyList<AdoConnectionDto>>;

/// <summary>
/// Get all email recipients
/// </summary>
public record GetEmailRecipientsQuery() : IRequest<IReadOnlyList<EmailRecipientDto>>;

// DTOs
public record ConfigurationDto(
    int ScanIntervalHours,
    bool EmailNotificationsEnabled,
    string MinimumSeverityForAlert,
    int MaxConcurrentScans,
    int RetentionDays,
    AzureConnectionDto? DefaultAzureConnection,
    AdoConnectionDto? DefaultAdoConnection,
    IReadOnlyList<EmailRecipientDto> EmailRecipients
);

public record AzureConnectionDto(
    string Id,
    string Name,
    string SubscriptionId,
    string ClientId,
    string Status,
    DateTimeOffset? LastTestedAt,
    string? LastTestError,
    bool IsDefault
);

public record AdoConnectionDto(
    string Id,
    string Name,
    string Organization,
    string Project,
    string Status,
    DateTimeOffset? LastTestedAt,
    string? LastTestError,
    bool IsDefault
);

public record EmailRecipientDto(
    string Id,
    string Email,
    string Name,
    bool IsActive,
    string MinimumSeverity,
    bool NotifyOnScanComplete,
    bool NotifyOnNewDrift
);
