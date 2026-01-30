using System.Text.Json;
using Chivato.Domain.Entities;
using Chivato.Domain.ValueObjects;

namespace Chivato.Infrastructure.TableEntities;

public class EmailRecipientTableEntity : BaseTableEntity
{
    public string Email { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public string PreferencesJson { get; set; } = "{}";

    public static EmailRecipientTableEntity FromDomain(EmailRecipient recipient)
    {
        return new EmailRecipientTableEntity
        {
            PartitionKey = recipient.TenantId,
            RowKey = recipient.Id,
            Email = recipient.Email,
            Name = recipient.Name,
            IsActive = recipient.IsActive,
            PreferencesJson = JsonSerializer.Serialize(recipient.Preferences),
            CreatedAt = recipient.CreatedAt,
            UpdatedAt = recipient.UpdatedAt
        };
    }

    public EmailRecipient ToDomain()
    {
        var preferences = JsonSerializer.Deserialize<NotificationPreferences>(PreferencesJson)
            ?? new NotificationPreferences();

        return EmailRecipient.Reconstitute(
            id: RowKey,
            tenantId: PartitionKey,
            email: Email,
            name: Name,
            isActive: IsActive,
            preferences: preferences,
            createdAt: CreatedAt,
            updatedAt: UpdatedAt
        );
    }
}
