using Microsoft.Extensions.DependencyInjection;

namespace Loom.Persistence.EntityFrameworkCore.Tests;

/// <summary>
/// Asserts each context's outbox keeps its own configuration.
/// </summary>
/// <remarks>
/// Registering the options directly would let the second call win, and the first context would run
/// with settings nobody chose for it — silently, since nothing about that looks like a failure.
/// </remarks>
public class OutboxConfigurationTests
{
    [Test]
    public async Task Two_Contexts_Keep_Their_Own_Settings()
    {
        ServiceCollection services = new();

        services.AddLoomPersistence(persistence => persistence
            .UseOutbox<TestDbContext>(outbox =>
            {
                outbox.MaximumAttempts = 3;
                outbox.BatchSize = 10;
            })
            .UseOutbox<OutboxDbContext>(outbox =>
            {
                outbox.MaximumAttempts = 7;
                outbox.BatchSize = 99;
            }));

        await using ServiceProvider provider = services.BuildServiceProvider();

        OutboxSettings<TestDbContext> first = provider.GetRequiredService<OutboxSettings<TestDbContext>>();
        OutboxSettings<OutboxDbContext> second = provider.GetRequiredService<OutboxSettings<OutboxDbContext>>();

        await Assert.That(first.Options.MaximumAttempts).IsEqualTo(3);
        await Assert.That(first.Options.BatchSize).IsEqualTo(10);
        await Assert.That(second.Options.MaximumAttempts).IsEqualTo(7);
        await Assert.That(second.Options.BatchSize).IsEqualTo(99);
    }

    [Test]
    public async Task Configuring_Two_Outboxes_Registers_One_Sink()
    {
        ServiceCollection services = new();

        services.AddLoomPersistence(persistence => persistence
            .UseOutbox<TestDbContext>()
            .UseOutbox<OutboxDbContext>());

        await using ServiceProvider provider = services.BuildServiceProvider();

        // The sink writes to whichever context is saving, so a second one would be a duplicate. The
        // interceptor takes the first of them, and two identical entries would be a confusing thing to
        // find while debugging.
        await Assert.That(provider.GetServices<IDeferredDomainEventSink>().Count()).IsEqualTo(1);
    }
}
