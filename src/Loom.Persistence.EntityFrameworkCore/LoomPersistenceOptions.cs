using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Loom.Persistence;

/// <summary>
/// Configures optional persistence behaviour.
/// </summary>
public sealed class LoomPersistenceOptions
{
    private readonly List<Action<IServiceCollection>> _registrations = [];

    /// <summary>
    /// Delivers deferred domain events through an outbox, after their transaction commits.
    /// </summary>
    /// <typeparam name="TContext">The context that holds the outbox table.</typeparam>
    /// <param name="configure">Adjusts batch size, retry limit, polling interval and event assemblies.</param>
    /// <returns>The same options, for chaining.</returns>
    /// <remarks>
    /// Without this, raising an <see cref="Loom.Entities.IDeferredDomainEvent" /> fails rather than
    /// being delivered immediately, because immediate delivery is a weaker guarantee than the event
    /// asked for.
    /// <para>
    /// The context must also map the table, via <see cref="OutboxModelBuilderExtensions.AddLoomOutbox" />.
    /// </para>
    /// </remarks>
    public LoomPersistenceOptions UseOutbox<TContext>(Action<OutboxOptions>? configure = null)
        where TContext : DbContext
    {
        OutboxOptions outbox = new();
        configure?.Invoke(outbox);

        _registrations.Add(services =>
        {
            services.TryAddSingleton(TimeProvider.System);
            services.AddSingleton(outbox);
            services.AddSingleton<OutboxEventSerializer>();
            services.AddScoped<IDeferredDomainEventSink, OutboxSink>();
            services.AddSingleton<OutboxProcessor<TContext>>();
            services.AddHostedService<OutboxDeliveryService<TContext>>();
        });

        return this;
    }

    internal void Apply(IServiceCollection services)
    {
        foreach (Action<IServiceCollection> registration in _registrations)
        {
            registration(services);
        }
    }
}
