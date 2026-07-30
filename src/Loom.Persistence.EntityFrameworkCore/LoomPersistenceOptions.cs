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
    /// <para>
    /// May be called for more than one context. Everything configurable is registered against the
    /// context it belongs to, so each outbox keeps its own settings rather than the last call winning.
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

            services.AddSingleton(new OutboxSettings<TContext>(outbox));
            services.AddSingleton<OutboxEventSerializer<TContext>>();
            services.AddSingleton<OutboxProcessor<TContext>>();
            services.AddHostedService<OutboxDeliveryService<TContext>>();

            // Scoped, because it works through the context. Registered whenever an outbox exists: a
            // deployment that cannot inspect or clear its outbox has no way to recover from either a
            // batch of failures or unbounded growth.
            services.AddScoped<OutboxAdministration<TContext>>();

            // Context-agnostic: it writes to whichever context is saving. Added once so that
            // configuring a second outbox does not register a duplicate.
            services.TryAddScoped<IDeferredDomainEventSink, OutboxSink>();
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
