using Chivato.Shared.Models.Messages;

namespace Chivato.Shared.Services;

/// <summary>
/// Abstraction for real-time notifications via SignalR.
/// </summary>
public interface ISignalRService
{
    /// <summary>
    /// Send a message to all users in a tenant group
    /// </summary>
    Task SendToTenantAsync(string tenantId, string target, object message);

    /// <summary>
    /// Send a message to a specific user
    /// </summary>
    Task SendToUserAsync(string userId, string target, object message);

    /// <summary>
    /// Send analysis progress update
    /// </summary>
    Task SendAnalysisProgressAsync(string tenantId, AnalysisProgressEvent progress);

    /// <summary>
    /// Send analysis completed notification
    /// </summary>
    Task SendAnalysisCompletedAsync(string tenantId, AnalysisCompletedEvent completed);

    /// <summary>
    /// Send analysis failed notification
    /// </summary>
    Task SendAnalysisFailedAsync(string tenantId, AnalysisFailedEvent failed);
}
