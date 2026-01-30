using Chivato.Shared.Models.Messages;
using Chivato.Shared.Services;
using Chivato.Worker.Handlers;
using Chivato.Worker.Processors;

var builder = Host.CreateApplicationBuilder(args);

// Configuration
var storageConnectionString = builder.Configuration["StorageConnectionString"]
    ?? "UseDevelopmentStorage=true";
var serviceBusConnectionString = builder.Configuration["ServiceBusConnectionString"];
var keyVaultUrl = builder.Configuration["KeyVaultUrl"]
    ?? "https://local-keyvault.vault.azure.net/";
var signalRConnectionString = builder.Configuration["AzureSignalRConnectionString"];

// Determine which message consumer to use
var useServiceBus = !string.IsNullOrEmpty(serviceBusConnectionString);

// Register shared services
builder.Services.AddSingleton<IStorageService>(_ => new StorageService(storageConnectionString));

// Key Vault service
if (!keyVaultUrl.Contains("local-keyvault"))
{
    builder.Services.AddSingleton<IKeyVaultService>(_ => new KeyVaultService(keyVaultUrl));
}
else
{
    builder.Services.AddSingleton<IKeyVaultService, MockKeyVaultService>();
}

// Azure Resource Service
builder.Services.AddSingleton<IAzureResourceService, AzureResourceService>();

// Azure DevOps Service
builder.Services.AddSingleton<IAdoService, AzureDevOpsService>();

// SignalR Service (optional - for real-time notifications)
if (!string.IsNullOrEmpty(signalRConnectionString))
{
    builder.Services.AddSingleton<ISignalRService>(_ => new SignalRService(signalRConnectionString));
}
else
{
    builder.Services.AddSingleton<ISignalRService, MockSignalRService>();
}

// Register processors
builder.Services.AddSingleton<IDriftAnalysisProcessor, DriftAnalysisProcessor>();

// Register message consumer as hosted service
// Use Service Bus for production, Storage Queue for development
if (useServiceBus)
{
    builder.Services.AddSingleton<IMessageConsumer>(sp => new ServiceBusMessageConsumer(
        serviceBusConnectionString!,
        sp.GetRequiredService<IDriftAnalysisProcessor>(),
        sp.GetRequiredService<IStorageService>(),
        sp.GetRequiredService<ISignalRService>(),
        sp.GetRequiredService<ILogger<ServiceBusMessageConsumer>>()
    ));
}
else
{
    builder.Services.AddSingleton<IMessageConsumer>(sp => new StorageQueueConsumer(
        storageConnectionString,
        sp.GetRequiredService<IDriftAnalysisProcessor>(),
        sp.GetRequiredService<IStorageService>(),
        sp.GetRequiredService<ISignalRService>(),
        sp.GetRequiredService<ILogger<StorageQueueConsumer>>()
    ));
}
builder.Services.AddHostedService(sp => (BackgroundService)sp.GetRequiredService<IMessageConsumer>());

// Application Insights
if (!string.IsNullOrEmpty(builder.Configuration["ApplicationInsights:ConnectionString"]))
{
    builder.Services.AddApplicationInsightsTelemetryWorkerService();
}

// Health checks
builder.Services.AddHealthChecks();

var host = builder.Build();

// Log startup
var logger = host.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("Chivato Worker starting...");
logger.LogInformation("Storage: {Storage}", storageConnectionString.Contains("Development") ? "Azurite" : "Azure");
logger.LogInformation("Queue: {Queue}", useServiceBus ? "Azure Service Bus" : "Azure Storage Queue (Azurite)");
logger.LogInformation("SignalR: {SignalR}", string.IsNullOrEmpty(signalRConnectionString) ? "Mock" : "Azure");

host.Run();

/// <summary>
/// Mock KeyVault service for local development
/// </summary>
public class MockKeyVaultService : IKeyVaultService
{
    public Task<string?> GetSecretAsync(string secretName)
    {
        return Task.FromResult<string?>(secretName switch
        {
            "ado-pat" => "mock-ado-pat-token",
            "azure-client-secret" => "mock-azure-client-secret",
            "azure-tenant-id" => "mock-tenant-id",
            "azure-client-id" => "mock-client-id",
            _ => null
        });
    }

    public Task SetSecretAsync(string secretName, string value, DateTimeOffset? expiresOn = null)
    {
        return Task.CompletedTask;
    }

    public Task DeleteSecretAsync(string secretName)
    {
        return Task.CompletedTask;
    }

    public Task<DateTimeOffset?> GetSecretExpirationAsync(string secretName)
    {
        // Mock secrets don't expire
        return Task.FromResult<DateTimeOffset?>(null);
    }
}

/// <summary>
/// Mock SignalR service for local development without Azure SignalR
/// </summary>
public class MockSignalRService : ISignalRService
{
    private readonly ILogger<MockSignalRService> _logger;

    public MockSignalRService(ILogger<MockSignalRService> logger)
    {
        _logger = logger;
    }

    public Task SendToTenantAsync(string tenantId, string target, object message)
    {
        _logger.LogInformation("Mock SignalR -> Tenant {TenantId}: {Target}", tenantId, target);
        return Task.CompletedTask;
    }

    public Task SendToUserAsync(string userId, string target, object message)
    {
        _logger.LogInformation("Mock SignalR -> User {UserId}: {Target}", userId, target);
        return Task.CompletedTask;
    }

    public Task SendAnalysisProgressAsync(string tenantId, AnalysisProgressEvent progress)
    {
        _logger.LogInformation("Mock SignalR -> Tenant {TenantId}: Progress {Stage} {Progress}%",
            tenantId, progress.Stage, progress.Progress);
        return Task.CompletedTask;
    }

    public Task SendAnalysisCompletedAsync(string tenantId, AnalysisCompletedEvent completed)
    {
        _logger.LogInformation("Mock SignalR -> Tenant {TenantId}: Analysis Completed, {Drifts} drifts found",
            tenantId, completed.Summary.TotalDrifts);
        return Task.CompletedTask;
    }

    public Task SendAnalysisFailedAsync(string tenantId, AnalysisFailedEvent failed)
    {
        _logger.LogWarning("Mock SignalR -> Tenant {TenantId}: Analysis Failed - {Error}",
            tenantId, failed.Error);
        return Task.CompletedTask;
    }
}
