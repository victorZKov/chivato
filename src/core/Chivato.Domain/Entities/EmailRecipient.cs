using Chivato.Domain.ValueObjects;

namespace Chivato.Domain.Entities;

/// <summary>
/// Email recipient for drift notifications
/// </summary>
public class EmailRecipient : BaseEntity
{
    public string Email { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public bool IsActive { get; private set; } = true;
    public NotificationPreferences Preferences { get; private set; } = new();

    private EmailRecipient() { }

    public static EmailRecipient Create(
        string tenantId,
        string email,
        string name,
        NotificationPreferences? preferences = null)
    {
        if (!IsValidEmail(email))
            throw new ArgumentException("Invalid email format", nameof(email));

        var recipient = new EmailRecipient
        {
            Id = Guid.NewGuid().ToString(),
            TenantId = tenantId,
            Email = email.ToLowerInvariant(),
            Name = name,
            IsActive = true,
            Preferences = preferences ?? new NotificationPreferences(),
            CreatedAt = DateTimeOffset.UtcNow
        };

        recipient.AddDomainEvent(new EmailRecipientAddedEvent(recipient.Id, recipient.TenantId, recipient.Email));

        return recipient;
    }

    public void Update(string name, NotificationPreferences preferences)
    {
        Name = name;
        Preferences = preferences;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Activate()
    {
        IsActive = true;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public bool ShouldReceiveNotification(Severity severity)
    {
        if (!IsActive) return false;

        return severity >= Preferences.MinimumSeverity;
    }

    private static bool IsValidEmail(string email)
    {
        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email;
        }
        catch
        {
            return false;
        }
    }
}

/// <summary>
/// Notification preferences for email recipient
/// </summary>
public class NotificationPreferences
{
    public Severity MinimumSeverity { get; set; } = Severity.High;
    public bool NotifyOnScanComplete { get; set; } = true;
    public bool NotifyOnScanFailed { get; set; } = true;
    public bool NotifyOnNewDrift { get; set; } = true;
    public bool DailyDigest { get; set; } = false;
    public bool WeeklyReport { get; set; } = true;
}

// Domain Events
public record EmailRecipientAddedEvent(string RecipientId, string TenantId, string Email) : IDomainEvent
{
    public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
}
