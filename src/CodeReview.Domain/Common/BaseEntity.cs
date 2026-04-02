namespace CodeReview.Domain.Common;

public abstract class BaseEntity
{
    public Guid Id { get; protected set; } = Guid.NewGuid();
    public DateTime CreatedAt { get; protected set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; protected set; }

    private readonly List<IDomainEvent> _domainEvents = [];
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void RaiseDomainEvent(IDomainEvent domainEvent) =>
        _domainEvents.Add(domainEvent);

    public void ClearDomainEvents() => _domainEvents.Clear();

    protected void SetUpdatedAt() => UpdatedAt = DateTime.UtcNow;
}

public abstract class AuditableEntity : BaseEntity
{
    public Guid? CreatedBy { get; protected set; }
    public Guid? UpdatedBy { get; protected set; }

    public void SetCreatedBy(Guid userId) => CreatedBy = userId;
    public void SetUpdatedBy(Guid userId)
    {
        UpdatedBy = userId;
        SetUpdatedAt();
    }
}
