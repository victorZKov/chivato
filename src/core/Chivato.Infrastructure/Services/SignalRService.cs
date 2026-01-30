using System.Text.Json;
using Chivato.Domain.Interfaces;
using Microsoft.Azure.SignalR.Management;
using Microsoft.Extensions.Logging;

namespace Chivato.Infrastructure.Services;

public class SignalRService : ISignalRService, IAsyncDisposable
{
    private readonly ServiceHubContext _hubContext;
    private readonly ILogger<SignalRService> _logger;
    private const string HubName = "notifications";

    public SignalRService(ServiceManager serviceManager, ILogger<SignalRService> logger)
    {
        _hubContext = serviceManager.CreateHubContextAsync(HubName, default).GetAwaiter().GetResult();
        _logger = logger;
    }

    public async Task SendToTenantAsync(string tenantId, string eventName, object payload, CancellationToken ct = default)
    {
        try
        {
            var groupName = $"tenant-{tenantId}";
            var jsonPayload = JsonSerializer.Serialize(payload);

            await _hubContext.Clients.Group(groupName).SendCoreAsync(eventName, new object[] { jsonPayload }, ct);

            _logger.LogDebug("Sent {EventName} to tenant {TenantId}", eventName, tenantId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending {EventName} to tenant {TenantId}", eventName, tenantId);
            throw;
        }
    }

    public async Task SendToUserAsync(string userId, string eventName, object payload, CancellationToken ct = default)
    {
        try
        {
            var jsonPayload = JsonSerializer.Serialize(payload);

            await _hubContext.Clients.User(userId).SendCoreAsync(eventName, new object[] { jsonPayload }, ct);

            _logger.LogDebug("Sent {EventName} to user {UserId}", eventName, userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending {EventName} to user {UserId}", eventName, userId);
            throw;
        }
    }

    public async Task AddUserToTenantGroupAsync(string userId, string tenantId, CancellationToken ct = default)
    {
        try
        {
            var groupName = $"tenant-{tenantId}";
            await _hubContext.UserGroups.AddToGroupAsync(userId, groupName, ct);

            _logger.LogDebug("Added user {UserId} to tenant group {TenantId}", userId, tenantId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding user {UserId} to tenant group {TenantId}", userId, tenantId);
            throw;
        }
    }

    public async Task RemoveUserFromTenantGroupAsync(string userId, string tenantId, CancellationToken ct = default)
    {
        try
        {
            var groupName = $"tenant-{tenantId}";
            await _hubContext.UserGroups.RemoveFromGroupAsync(userId, groupName, ct);

            _logger.LogDebug("Removed user {UserId} from tenant group {TenantId}", userId, tenantId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing user {UserId} from tenant group {TenantId}", userId, tenantId);
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _hubContext.DisposeAsync();
    }
}
