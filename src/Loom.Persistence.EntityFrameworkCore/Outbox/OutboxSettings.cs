using Microsoft.EntityFrameworkCore;

namespace Loom.Persistence;

/// <summary>
/// One context's outbox configuration.
/// </summary>
/// <typeparam name="TContext">The context the settings belong to.</typeparam>
/// <remarks>
/// Generic over the context on purpose. Registering <see cref="OutboxOptions" /> directly would give
/// every outbox in the application the same batch size, retry limit and event assemblies — with two
/// contexts, the second registration would simply win, and the first context's configuration would
/// silently be ignored. Tying the settings to the context keeps them apart without keyed lookups.
/// </remarks>
/// <param name="options">The configuration this context's outbox was given.</param>
public sealed class OutboxSettings<TContext>(OutboxOptions options)
    where TContext : DbContext
{
    /// <summary>
    /// Gets the configuration for this context's outbox.
    /// </summary>
    public OutboxOptions Options { get; } = options;
}
