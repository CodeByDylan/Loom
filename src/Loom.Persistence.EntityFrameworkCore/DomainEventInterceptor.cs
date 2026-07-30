using Loom.Entities;
using Loom.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Loom.Persistence;

/// <summary>
/// Drains domain events from changed aggregates and dispatches them as part of the save.
/// </summary>
/// <remarks>
/// Dispatch happens <em>before</em> the save executes, so anything a handler changes joins the same
/// save and the whole operation is atomic. A handler failure abandons the save, so a rule that was
/// meant to react to a change cannot silently fail to react to a change that was nonetheless
/// committed.
/// <para>
/// The consequences are deliberate and worth knowing: a handler cannot read committed state, must not
/// call <c>SaveChanges</c> itself, and must not reach outside the process, because a later rollback
/// cannot undo an email. An event whose handlers need any of those is a
/// <see cref="IDeferredDomainEvent" />.
/// </para>
/// </remarks>
/// <param name="dispatcher">Invokes the handlers for an event.</param>
/// <param name="deferredSinks">
/// Records deferred events, when an outbox is configured. Injected as a sequence because a container
/// always resolves an empty one when nothing is registered, so "no outbox" needs no separate
/// registration.
/// </param>
public sealed class DomainEventInterceptor(
    IDomainEventDispatcher dispatcher,
    IEnumerable<IDeferredDomainEventSink> deferredSinks) : SaveChangesInterceptor
{
    private readonly IDeferredDomainEventSink? _deferredSink = deferredSinks.FirstOrDefault();

    /// <summary>
    /// How many times events raised by handlers are drained before giving up.
    /// </summary>
    /// <remarks>
    /// A handler may raise further events, so draining repeats. The limit turns a cycle of two
    /// aggregates raising events at each other into a diagnosable error rather than a hang.
    /// </remarks>
    public const int MaximumDrainPasses = 10;

    /// <inheritdoc />
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        if (eventData.Context is not null)
        {
            await DrainAsync(eventData.Context, cancellationToken);
        }

        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private async Task DrainAsync(DbContext context, CancellationToken cancellationToken)
    {
        for (int pass = 0; pass < MaximumDrainPasses; pass++)
        {
            IDomainEvent[] events = Drain(context);

            if (events.Length is 0)
            {
                return;
            }

            foreach (IDomainEvent domainEvent in events)
            {
                if (domainEvent is IDeferredDomainEvent deferred)
                {
                    Defer(context, deferred);
                    continue;
                }

                Result dispatched = await dispatcher.DispatchAsync(domainEvent, cancellationToken);

                if (dispatched.IsFailure)
                {
                    throw new DomainEventDispatchException(dispatched.Error, domainEvent.GetType());
                }
            }
        }

        throw new InvalidOperationException(
            $"Domain events were still being raised after {MaximumDrainPasses} passes. A handler is "
            + "raising an event that leads back to itself.");
    }

    private static IDomainEvent[] Drain(DbContext context) =>
    [
        .. context.ChangeTracker
            .Entries<IAggregateRoot>()
            .Select(entry => entry.Entity)
            .Where(aggregate => aggregate.DomainEvents.Count > 0)
            .ToArray()
            .SelectMany(aggregate => aggregate.DequeueDomainEvents()),
    ];

    private void Defer(DbContext context, IDeferredDomainEvent deferred)
    {
        if (_deferredSink is null)
        {
            throw new InvalidOperationException(
                $"'{deferred.GetType().Name}' is a deferred domain event, but no outbox is configured. "
                + "Call UseOutbox when registering Loom persistence, or make the event an ordinary "
                + "IDomainEvent so that it is dispatched inside the transaction.");
        }

        // Written through the same context, so the record of what is owed commits atomically with the
        // change that owed it. That is the entire point of an outbox.
        _deferredSink.Enqueue(context, deferred);
    }
}
