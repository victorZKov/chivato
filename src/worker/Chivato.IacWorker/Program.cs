using Chivato.IacWorker.Handlers;
using Chivato.IacWorker.Processors;
using Chivato.Shared.Services;

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

// SignalR Service (optional - for real-time notifications)
if (!string.IsNullOrEmpty(signalRConnectionString))
{
    builder.Services.AddSingleton<ISignalRService>(_ => new SignalRService(signalRConnectionString));
}
else
{
    builder.Services.AddSingleton<ISignalRService, MockSignalRService>();
}

// Check if we need to register IEmailService
// Assuming it's in shared now
builder.Services.AddSingleton<IEmailService, EmailService>();

// Azure DevOps service for triggering pipelines and getting logs
builder.Services.AddSingleton<IAdoService, AzureDevOpsService>();

// Terraform Plan Analyzer (AI-powered)
var openAiEndpoint = builder.Configuration["AZURE_OPENAI_ENDPOINT"] 
    ?? builder.Configuration["AzureOpenAI:Endpoint"];
var openAiDeployment = builder.Configuration["AZURE_OPENAI_DEPLOYMENT"] 
    ?? builder.Configuration["AzureOpenAI:Deployment"]
    ?? "gpt-4o";
var openAiApiKey = builder.Configuration["AZURE_OPENAI_API_KEY"] 
    ?? builder.Configuration["AzureOpenAI:ApiKey"];

if (!string.IsNullOrEmpty(openAiEndpoint) && !string.IsNullOrEmpty(openAiApiKey))
{
    Console.WriteLine($"AI Analyzer: Azure OpenAI ({openAiEndpoint})");
    builder.Services.AddSingleton<ITerraformPlanAnalyzer>(_ => 
        new TerraformPlanAnalyzer(openAiEndpoint, openAiDeployment, openAiApiKey));
}
else
{
    Console.WriteLine("AI Analyzer: Mock (no Azure OpenAI configured)");
    builder.Services.AddSingleton<ITerraformPlanAnalyzer, MockTerraformPlanAnalyzer>();
}

// Register processors
builder.Services.AddSingleton<IIacAnalysisProcessor, IacAnalysisProcessor>();

// Register message consumer as hosted service
if (useServiceBus)
{
    builder.Services.AddSingleton<IIacMessageConsumer>(sp => new ServiceBusIacConsumer(
        serviceBusConnectionString!,
        sp.GetRequiredService<IIacAnalysisProcessor>(),
        sp.GetRequiredService<IStorageService>(),
        sp.GetRequiredService<ISignalRService>(),
        sp.GetRequiredService<ILogger<ServiceBusIacConsumer>>()
    ));
}
else
{
    builder.Services.AddSingleton<IIacMessageConsumer>(sp => new StorageQueueIacConsumer(
        storageConnectionString,
        sp.GetRequiredService<IIacAnalysisProcessor>(),
        sp.GetRequiredService<IStorageService>(),
        sp.GetRequiredService<ISignalRService>(),
        sp.GetRequiredService<ILogger<StorageQueueIacConsumer>>()
    ));
}
builder.Services.AddHostedService(sp => (BackgroundService)sp.GetRequiredService<IIacMessageConsumer>());

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
logger.LogInformation("Chivato IaC Worker starting...");
logger.LogInformation("Storage: {Storage}", storageConnectionString.Contains("Development") ? "Azurite" : "Azure");
logger.LogInformation("Queue: {Queue}", useServiceBus ? "Azure Service Bus" : "Azure Storage Queue (Azurite)");
logger.LogInformation("SignalR: {SignalR}", string.IsNullOrEmpty(signalRConnectionString) ? "Mock" : "Azure");
logger.LogInformation("AI Analyzer: {AI}", !string.IsNullOrEmpty(openAiEndpoint) ? "Azure OpenAI" : "Mock");
logger.LogInformation("Mode: Pipeline-based drift detection (triggers ADO pipelines for terraform plan)");

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
            "ado-pat" => Environment.GetEnvironmentVariable("ADO_PAT") ?? "mock-ado-pat-token",
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
        return Task.FromResult<DateTimeOffset?>(null);
    }
}

/// <summary>
/// Mock SignalR service for local development
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

    public Task SendAnalysisProgressAsync(string tenantId, Chivato.Shared.Models.Messages.AnalysisProgressEvent progress)
    {
        _logger.LogInformation("Mock SignalR -> Tenant {TenantId}: Progress {Stage} {Progress}%",
            tenantId, progress.Stage, progress.Progress);
        return Task.CompletedTask;
    }

    public Task SendAnalysisCompletedAsync(string tenantId, Chivato.Shared.Models.Messages.AnalysisCompletedEvent completed)
    {
        _logger.LogInformation("Mock SignalR -> Tenant {TenantId}: Analysis Completed, {Drifts} drifts found",
            tenantId, completed.Summary.TotalDrifts);
        return Task.CompletedTask;
    }

    public Task SendAnalysisFailedAsync(string tenantId, Chivato.Shared.Models.Messages.AnalysisFailedEvent failed)
    {
        _logger.LogWarning("Mock SignalR -> Tenant {TenantId}: Analysis Failed - {Error}",
            tenantId, failed.Error);
        return Task.CompletedTask;
    }

    public Task SendDriftDetectedAsync(string tenantId, Chivato.Shared.Models.DriftAnalysisResult result)
    {
        _logger.LogInformation("Mock SignalR -> Tenant {TenantId}: Drift Detected, {Risk} risk",
            tenantId, result.OverallRisk);
        return Task.CompletedTask;
    }
}
