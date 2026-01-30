using Azure.Data.Tables;
using Chivato.Domain.Entities;
using Chivato.Domain.Interfaces;
using Chivato.Domain.ValueObjects;
using Chivato.Infrastructure.TableEntities;

namespace Chivato.Infrastructure.Repositories;

public class EmailRecipientRepository : IEmailRecipientRepository
{
    private readonly TableClient _tableClient;
    private const string TableName = "EmailRecipients";

    public EmailRecipientRepository(TableServiceClient tableServiceClient)
    {
        _tableClient = tableServiceClient.GetTableClient(TableName);
        _tableClient.CreateIfNotExists();
    }

    public async Task<EmailRecipient?> GetByIdAsync(string tenantId, string id, CancellationToken ct = default)
    {
        try
        {
            var response = await _tableClient.GetEntityAsync<EmailRecipientTableEntity>(tenantId, id, cancellationToken: ct);
            return response.Value.ToDomain();
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task<EmailRecipient?> GetByEmailAsync(string tenantId, string email, CancellationToken ct = default)
    {
        var filter = $"PartitionKey eq '{tenantId}' and Email eq '{email.ToLowerInvariant()}'";
        var query = _tableClient.QueryAsync<EmailRecipientTableEntity>(filter: filter, maxPerPage: 1, cancellationToken: ct);

        await foreach (var entity in query)
        {
            return entity.ToDomain();
        }

        return null;
    }

    public async Task<IReadOnlyList<EmailRecipient>> GetAllAsync(string tenantId, CancellationToken ct = default)
    {
        var recipients = new List<EmailRecipient>();
        var filter = $"PartitionKey eq '{tenantId}'";
        var query = _tableClient.QueryAsync<EmailRecipientTableEntity>(filter: filter, cancellationToken: ct);

        await foreach (var entity in query)
        {
            recipients.Add(entity.ToDomain());
        }

        return recipients;
    }

    public async Task<IReadOnlyList<EmailRecipient>> GetActiveAsync(string tenantId, CancellationToken ct = default)
    {
        var recipients = new List<EmailRecipient>();
        var filter = $"PartitionKey eq '{tenantId}' and IsActive eq true";
        var query = _tableClient.QueryAsync<EmailRecipientTableEntity>(filter: filter, cancellationToken: ct);

        await foreach (var entity in query)
        {
            recipients.Add(entity.ToDomain());
        }

        return recipients;
    }

    public async Task<IReadOnlyList<EmailRecipient>> GetForNotificationAsync(string tenantId, Severity severity, CancellationToken ct = default)
    {
        // Get all active recipients and filter by severity in memory
        // (Table Storage doesn't support JSON property filtering)
        var allActive = await GetActiveAsync(tenantId, ct);
        return allActive.Where(r => r.ShouldReceiveNotification(severity)).ToList();
    }

    public async Task AddAsync(EmailRecipient recipient, CancellationToken ct = default)
    {
        var entity = EmailRecipientTableEntity.FromDomain(recipient);
        await _tableClient.AddEntityAsync(entity, ct);
    }

    public async Task UpdateAsync(EmailRecipient recipient, CancellationToken ct = default)
    {
        var entity = EmailRecipientTableEntity.FromDomain(recipient);
        await _tableClient.UpsertEntityAsync(entity, TableUpdateMode.Replace, ct);
    }

    public async Task DeleteAsync(string tenantId, string id, CancellationToken ct = default)
    {
        await _tableClient.DeleteEntityAsync(tenantId, id, cancellationToken: ct);
    }
}
