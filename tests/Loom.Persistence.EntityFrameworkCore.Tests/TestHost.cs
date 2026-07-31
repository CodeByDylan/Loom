using Microsoft.Extensions.DependencyInjection;

namespace Loom.Persistence.EntityFrameworkCore.Tests;

/// <summary>
/// The wiring a consumer would use: the context resolved from the container with the interceptor
/// attached, so an event handler receives the same scoped context the save is running on.
/// </summary>
internal sealed class TestHost : SqliteHost
{
    internal static async Task<TestHost> CreateAsync(Action<IServiceCollection>? configure = null)
    {
        TestHost host = new();

        await host.InitialiseAsync<TestDbContext>(services =>
        {
            services.AddLoomPersistence();
            configure?.Invoke(services);
        });

        return host;
    }
}
