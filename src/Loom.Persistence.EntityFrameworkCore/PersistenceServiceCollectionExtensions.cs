using Microsoft.Extensions.DependencyInjection;

namespace Loom.Persistence;

/// <summary>
/// Registers Loom's persistence services.
/// </summary>
public static class PersistenceServiceCollectionExtensions
{
    /// <summary>
    /// Registers domain event dispatch, and whatever else the given options ask for.
    /// </summary>
    /// <param name="services">The service collection to populate.</param>
    /// <param name="configure">Configures optional behaviour, such as the outbox.</param>
    /// <returns>The same collection, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services" /> is <see langword="null" />.</exception>
    /// <remarks>
    /// The interceptor still has to be attached to the context, which is the consumer's call because
    /// it is the consumer that chooses the provider:
    /// <example>
    /// <code>
    /// services.AddLoomPersistence();
    /// services.AddDbContext&lt;AppDbContext&gt;((serviceProvider, options) => options
    ///     .UseNpgsql(connectionString)
    ///     .AddInterceptors(serviceProvider.GetRequiredService&lt;DomainEventInterceptor&gt;()));
    /// </code>
    /// </example>
    /// </remarks>
    public static IServiceCollection AddLoomPersistence(
        this IServiceCollection services,
        Action<LoomPersistenceOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        LoomPersistenceOptions options = new();
        configure?.Invoke(options);

        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        services.AddScoped<DomainEventInterceptor>();

        options.Apply(services);

        return services;
    }
}
