using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Chivato.Functions.Services;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

// Configure services
builder.Services.AddApplicationInsightsTelemetryWorkerService();
builder.Services.ConfigureFunctionsApplicationInsights();

// Register Chivato services
var storageConnectionString = Environment.GetEnvironmentVariable("StorageConnectionString")
    ?? "UseDevelopmentStorage=true";
var keyVaultUrl = Environment.GetEnvironmentVariable("KeyVaultUrl")
    ?? "https://your-keyvault.vault.azure.net/";
var serviceBusConnectionString = Environment.GetEnvironmentVariable("ServiceBusConnectionString");

builder.Services.AddSingleton<IStorageService>(_ => new StorageService(storageConnectionString));
builder.Services.AddSingleton<IKeyVaultService>(_ => new KeyVaultService(keyVaultUrl));

// Message Queue Service (for triggering worker)
if (!string.IsNullOrEmpty(serviceBusConnectionString))
{
    builder.Services.AddSingleton<Chivato.Shared.Services.IMessageQueueService>(
        _ => new Chivato.Shared.Services.ServiceBusMessageQueueService(serviceBusConnectionString));
}
else
{
    // Fallback or warning - for now we register null or throw if critical
    // Ideally we should have a mock or check connection string presence
}

// Core services
builder.Services.AddSingleton<IAdoService, AdoService>();
builder.Services.AddSingleton<IAzureResourceService, AzureResourceService>();
builder.Services.AddSingleton<IAiAnalyzerService, AiAnalyzerService>();
builder.Services.AddSingleton<IEmailService, EmailService>();

// Billing services
builder.Services.AddSingleton<Chivato.Functions.Services.Billing.IBillingStorageService>(
    _ => new Chivato.Functions.Services.Billing.BillingStorageService(storageConnectionString));

// Configure CORS for local development
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

builder.Build().Run();
