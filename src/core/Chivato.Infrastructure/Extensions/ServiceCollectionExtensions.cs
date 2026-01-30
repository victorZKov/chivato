using Azure.Data.Tables;
using Azure.Identity;
using Azure.Messaging.ServiceBus;
using Azure.ResourceManager;
using Azure.Security.KeyVault.Secrets;
using Chivato.Domain.Interfaces;
using Chivato.Infrastructure.Repositories;
using Chivato.Infrastructure.Services;
using Microsoft.Azure.SignalR.Management;
using Microsoft.Extensions.DependencyInjection;

namespace Chivato.Infrastructure.Extensions;

public class InfrastructureOptions
{
    public string StorageConnectionString { get; set; } = string.Empty;
    public string KeyVaultUrl { get; set; } = string.Empty;
    public string ServiceBusConnectionString { get; set; } = string.Empty;
    public string SignalRConnectionString { get; set; } = string.Empty;
    public string SendGridApiKey { get; set; } = string.Empty;
    public string EmailFromAddress { get; set; } = "noreply@chivato.io";
    public string EmailFromName { get; set; } = "Chivato";
}

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        InfrastructureOptions options)
    {
        // Azure Table Storage
        services.AddSingleton(_ => new TableServiceClient(options.StorageConnectionString));

        // Azure Key Vault
        if (!string.IsNullOrEmpty(options.KeyVaultUrl))
        {
            services.AddSingleton(_ => new SecretClient(new Uri(options.KeyVaultUrl), new DefaultAzureCredential()));
            services.AddSingleton<IKeyVaultService, KeyVaultService>();
        }

        // Azure Resource Manager
        services.AddSingleton(_ => new ArmClient(new DefaultAzureCredential()));
        services.AddSingleton<IAzureResourceService, AzureResourceService>();

        // Azure Service Bus
        if (!string.IsNullOrEmpty(options.ServiceBusConnectionString))
        {
            services.AddSingleton(_ => new ServiceBusClient(options.ServiceBusConnectionString));
            services.AddSingleton<IMessageQueueService, MessageQueueService>();
        }

        // Azure SignalR
        if (!string.IsNullOrEmpty(options.SignalRConnectionString))
        {
            services.AddSingleton(_ =>
                new ServiceManagerBuilder()
                    .WithOptions(o => o.ConnectionString = options.SignalRConnectionString)
                    .BuildServiceManager());
            services.AddSingleton<ISignalRService, SignalRService>();
        }

        // SendGrid Email
        if (!string.IsNullOrEmpty(options.SendGridApiKey))
        {
            services.Configure<EmailServiceOptions>(o =>
            {
                o.ApiKey = options.SendGridApiKey;
                o.FromEmail = options.EmailFromAddress;
                o.FromName = options.EmailFromName;
            });
            services.AddSingleton<IEmailService, EmailService>();
        }

        // Azure DevOps Service
        services.AddHttpClient<IAdoService, AdoService>();

        // Repositories
        services.AddScoped<IPipelineRepository, PipelineRepository>();
        services.AddScoped<IDriftRecordRepository, DriftRecordRepository>();
        services.AddScoped<IScanLogRepository, ScanLogRepository>();
        services.AddScoped<IAzureConnectionRepository, AzureConnectionRepository>();
        services.AddScoped<IAdoConnectionRepository, AdoConnectionRepository>();
        services.AddScoped<IConfigurationRepository, ConfigurationRepository>();
        services.AddScoped<ICredentialRepository, CredentialRepository>();
        services.AddScoped<IEmailRecipientRepository, EmailRecipientRepository>();

        return services;
    }

    /// <summary>
    /// Simple overload for development with only storage connection
    /// </summary>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        string storageConnectionString)
    {
        return services.AddInfrastructure(new InfrastructureOptions
        {
            StorageConnectionString = storageConnectionString
        });
    }
}
