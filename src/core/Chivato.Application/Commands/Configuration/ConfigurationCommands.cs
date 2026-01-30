using MediatR;

namespace Chivato.Application.Commands.Configuration;

// ==========================================
// Timer/Settings Commands
// ==========================================

public record UpdateTimerCommand(int IntervalHours) : IRequest<CommandResult>;

public record UpdateSettingsCommand(
    string MinimumSeverityForAlert,
    int MaxConcurrentScans,
    int RetentionDays,
    bool EmailNotificationsEnabled = true
) : IRequest<CommandResult>;

// ==========================================
// Azure Connection Commands
// ==========================================

public record SaveAzureConnectionCommand(
    string? Id,
    string Name,
    string TenantId,
    string SubscriptionId,
    string ClientId,
    string ClientSecret,
    bool IsDefault = false
) : IRequest<SaveConnectionResult>;

public record DeleteAzureConnectionCommand(string Id) : IRequest<CommandResult>;

public record TestAzureConnectionCommand(string Id) : IRequest<TestConnectionResult>;

public record SetDefaultAzureConnectionCommand(string Id) : IRequest<CommandResult>;

// ==========================================
// ADO Connection Commands
// ==========================================

public record SaveAdoConnectionCommand(
    string? Id,
    string Name,
    string Organization,
    string Project,
    string PatToken,
    bool IsDefault = false
) : IRequest<SaveConnectionResult>;

public record DeleteAdoConnectionCommand(string Id) : IRequest<CommandResult>;

public record TestAdoConnectionCommand(string Id) : IRequest<TestConnectionResult>;

public record SetDefaultAdoConnectionCommand(string Id) : IRequest<CommandResult>;

// ==========================================
// Email Recipient Commands
// ==========================================

public record AddEmailRecipientCommand(
    string Email,
    string Name,
    string MinimumSeverity,
    bool NotifyOnScanComplete = true,
    bool NotifyOnNewDrift = true
) : IRequest<SaveConnectionResult>;

public record UpdateEmailRecipientCommand(
    string Id,
    string Name,
    string MinimumSeverity,
    bool NotifyOnScanComplete,
    bool NotifyOnNewDrift,
    bool IsActive
) : IRequest<CommandResult>;

public record RemoveEmailRecipientCommand(string Id) : IRequest<CommandResult>;

// ==========================================
// Common Result Types
// ==========================================

public record CommandResult(bool Success, string? ErrorMessage = null);

public record SaveConnectionResult(string Id, bool Success, string? ErrorMessage = null);

public record TestConnectionResult(bool Success, string Status, string? ErrorMessage = null);
