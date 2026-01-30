using Chivato.Domain.Entities;
using Chivato.Domain.ValueObjects;

namespace Chivato.Domain.Interfaces;

public interface IEmailRecipientRepository
{
    Task<EmailRecipient?> GetByIdAsync(string tenantId, string id, CancellationToken ct = default);
    Task<EmailRecipient?> GetByEmailAsync(string tenantId, string email, CancellationToken ct = default);
    Task<IReadOnlyList<EmailRecipient>> GetAllAsync(string tenantId, CancellationToken ct = default);
    Task<IReadOnlyList<EmailRecipient>> GetActiveAsync(string tenantId, CancellationToken ct = default);
    Task<IReadOnlyList<EmailRecipient>> GetForNotificationAsync(string tenantId, Severity severity, CancellationToken ct = default);
    Task AddAsync(EmailRecipient recipient, CancellationToken ct = default);
    Task UpdateAsync(EmailRecipient recipient, CancellationToken ct = default);
    Task DeleteAsync(string tenantId, string id, CancellationToken ct = default);
}
