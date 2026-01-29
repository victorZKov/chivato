using Azure.Data.Tables;
using Chivato.Application.Commands.Analysis;
using Chivato.Domain.Interfaces;
using Chivato.Infrastructure.MessageQueue;
using Chivato.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Chivato.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        string storageConnectionString,
        string serviceBusConnectionString)
    {
        // Azure Table Storage
        services.AddSingleton(_ => new TableServiceClient(storageConnectionString));

        // Repositories
        services.AddScoped<IPipelineRepository, PipelineRepository>();

        // Message Queue
        if (!string.IsNullOrEmpty(serviceBusConnectionString))
        {
            services.AddSingleton<IMessageQueueService>(_ => new ServiceBusMessageQueue(serviceBusConnectionString));
        }

        return services;
    }
}
