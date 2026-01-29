namespace Chivato.Domain.Entities;

/// <summary>
/// Base entity for all domain entities
/// </summary>
public abstract class BaseEntity
{
    public string Id { get; protected set; } = string.Empty;
    public string TenantId { get; protected set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; protected set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; protected set; }

    private readonly List<IDomainEvent> _domainEvents = new();
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void AddDomainEvent(IDomainEvent domainEvent)
    {
        _domainEvents.Add(domainEvent);
    }

    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }
}

/// <summary>
/// Marker interface for domain events
/// </summary>
public interface IDomainEvent
{
    DateTimeOffset OccurredAt { get; }
}
