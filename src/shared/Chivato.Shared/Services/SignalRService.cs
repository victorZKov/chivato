using Microsoft.Azure.SignalR.Management;
using Chivato.Shared.Models.Messages;

namespace Chivato.Shared.Services;

/// <summary>
/// Azure SignalR Service implementation for real-time notifications
/// </summary>
public class SignalRService : ISignalRService, IAsyncDisposable
{
    private readonly ServiceManager _serviceManager;
    private ServiceHubContext? _hubContext;
    private const string HubName = "chivato";

    public SignalRService(string connectionString)
    {
        _serviceManager = new ServiceManagerBuilder()
            .WithOptions(opt =>
            {
                opt.ConnectionString = connectionString;
                opt.ServiceTransportType = ServiceTransportType.Persistent;
            })
            .BuildServiceManager();
    }

    private async Task<ServiceHubContext> GetHubContextAsync()
    {
        if (_hubContext == null)
        {
            _hubContext = await _serviceManager.CreateHubContextAsync(HubName, default);
        }
        return _hubContext;
    }

    public async Task SendToTenantAsync(string tenantId, string target, object message)
    {
        var hubContext = await GetHubContextAsync();
        await hubContext.Clients.Group($"tenant-{tenantId}").SendCoreAsync(target, new[] { message });
    }

    public async Task SendToUserAsync(string userId, string target, object message)
    {
        var hubContext = await GetHubContextAsync();
        await hubContext.Clients.User(userId).SendCoreAsync(target, new[] { message });
    }

    public async Task SendAnalysisProgressAsync(string tenantId, AnalysisProgressEvent progress)
    {
        await SendToTenantAsync(tenantId, "analysisProgress", progress);
    }

    public async Task SendAnalysisCompletedAsync(string tenantId, AnalysisCompletedEvent completed)
    {
        await SendToTenantAsync(tenantId, "analysisCompleted", completed);
    }

    public async Task SendAnalysisFailedAsync(string tenantId, AnalysisFailedEvent failed)
    {
        await SendToTenantAsync(tenantId, "analysisFailed", failed);
    }

    public async ValueTask DisposeAsync()
    {
        if (_hubContext != null)
        {
            await _hubContext.DisposeAsync();
        }
        // ServiceManager doesn't implement IAsyncDisposable, use sync dispose
        _serviceManager.Dispose();
        GC.SuppressFinalize(this);
    }
}
