using System.Collections.Concurrent;
using System.Reflection;
using Loom.Entities;
using Loom.Results;
using Microsoft.Extensions.DependencyInjection;

namespace Loom.Persistence;

/// <remarks>
/// An event is only known as <see cref="IDomainEvent" /> at the point of dispatch, so the closed
/// handler type has to be constructed at run time. The reflection involved is cached per event type,
/// which keeps it to once per type for the lifetime of the process rather than once per event.
/// </remarks>
internal sealed class DomainEventDispatcher(IServiceProvider services) : IDomainEventDispatcher
{
    private static readonly ConcurrentDictionary<Type, EventTypePlan> Plans = new();

    public async Task<Result> DispatchAsync(IDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        EventTypePlan plan = Plans.GetOrAdd(domainEvent.GetType(), static type => new EventTypePlan(type));

        foreach (object handler in plan.ResolveHandlers(services))
        {
            Result result = await plan.InvokeAsync(handler, domainEvent, cancellationToken);

            // Stops at the first failure. For an ordinary event the transaction is about to be
            // abandoned anyway, so running the remaining handlers would do work that is discarded.
            if (result.IsFailure)
            {
                return result;
            }
        }

        return Result.Success;
    }

    private sealed class EventTypePlan
    {
        private readonly Type _handlerCollectionType;
        private readonly MethodInfo _handleMethod;

        internal EventTypePlan(Type eventType)
        {
            Type handlerType = typeof(IDomainEventHandler<>).MakeGenericType(eventType);
            _handlerCollectionType = typeof(IEnumerable<>).MakeGenericType(handlerType);
            _handleMethod = handlerType.GetMethod(nameof(IDomainEventHandler<IDomainEvent>.HandleAsync))!;
        }

        internal IEnumerable<object> ResolveHandlers(IServiceProvider services) =>
            ((IEnumerable<object>)services.GetRequiredService(_handlerCollectionType)).ToArray();

        internal Task<Result> InvokeAsync(object handler, IDomainEvent domainEvent, CancellationToken cancellationToken) =>
            (Task<Result>)_handleMethod.Invoke(handler, [domainEvent, cancellationToken])!;
    }
}
