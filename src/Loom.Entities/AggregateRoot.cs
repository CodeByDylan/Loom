namespace Loom.Entities;

/// <summary>
/// An <see cref="Entity{TSelf}" /> that is the entry point to an aggregate and records domain events.
/// </summary>
/// <typeparam name="TSelf">The deriving aggregate type.</typeparam>
/// <remarks>
/// This package collects domain events but does not dispatch them: dispatch needs a save-changes
/// interceptor, which means an object-relational mapper, which is a higher tier than this one — and a
/// package never references upwards. So events accumulate here and something above drains them.
/// <para>
/// <c>Loom.Persistence.EntityFrameworkCore</c> is what does, through its save-changes interceptor.
/// Without it, a consumer drains the collection itself.
/// </para>
/// <para>
/// Not sealed, for the same reason as <see cref="Entity{TSelf}" />.
/// </para>
/// </remarks>
public abstract class AggregateRoot<TSelf> : Entity<TSelf>, IAggregateRoot
    where TSelf : AggregateRoot<TSelf>
{
    private readonly List<IDomainEvent> _domainEvents = [];

    /// <summary>
    /// Creates an aggregate with a new identity.
    /// </summary>
    protected AggregateRoot()
    {
    }

    /// <summary>
    /// Reconstructs an aggregate with a known identity.
    /// </summary>
    /// <param name="id">The stored identity.</param>
    protected AggregateRoot(Id<TSelf> id) : base(id)
    {
    }

    /// <inheritdoc />
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents;

    /// <summary>
    /// Records that something happened.
    /// </summary>
    /// <param name="domainEvent">What happened.</param>
    /// <exception cref="ArgumentNullException"><paramref name="domainEvent" /> is <see langword="null" />.</exception>
    /// <remarks>
    /// Protected, so only the aggregate can record its own events. Nothing outside can inject one,
    /// which is what makes the recorded events trustworthy.
    /// </remarks>
    protected void Raise(IDomainEvent domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        _domainEvents.Add(domainEvent);
    }

    /// <inheritdoc />
    public IReadOnlyCollection<IDomainEvent> DequeueDomainEvents()
    {
        if (_domainEvents.Count is 0)
        {
            return [];
        }

        IDomainEvent[] drained = [.. _domainEvents];
        _domainEvents.Clear();
        return drained;
    }
}
