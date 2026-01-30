using Azure.Data.Tables;
using Chivato.Domain.Interfaces;
using Chivato.Infrastructure.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace Chivato.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string storageConnectionString)
    {
        // Register TableServiceClient
        services.AddSingleton(_ => new TableServiceClient(storageConnectionString));

        // Register Repositories
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
}
