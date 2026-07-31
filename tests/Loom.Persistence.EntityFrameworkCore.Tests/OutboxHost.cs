using Loom.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Loom.Persistence.EntityFrameworkCore.Tests;

/// <summary>
/// A host with the outbox configured, driving delivery by hand rather than on a timer.
/// </summary>
internal sealed class OutboxHost : SqliteHost
{
    internal static async Task<OutboxHost> CreateAsync(bool failing = false, int maximumAttempts = 5)
    {
        OutboxHost host = new();

        await host.InitialiseAsync<OutboxDbContext>(services =>
        {
            services.AddLoomPersistence(options => options.UseOutbox<OutboxDbContext>(outbox =>
            {
                outbox.MaximumAttempts = maximumAttempts;
                outbox.EventAssemblies = [typeof(OrderShipped).Assembly];
            }));

            services.AddScoped<IDomainEventHandler<OrderCancelled>, OutboxTests.CancelledHandler>();

            if (failing)
            {
                services.AddScoped<IDomainEventHandler<OrderShipped>, OutboxTests.FailingShippedHandler>();
            }
            else
            {
                services.AddScoped<IDomainEventHandler<OrderShipped>, OutboxTests.ShippedHandler>();
            }
        });

        return host;
    }

    internal OutboxProcessor<OutboxDbContext> Processor => Resolve<OutboxProcessor<OutboxDbContext>>();

    internal async Task InScopeAsync(Func<OutboxDbContext, Task> work)
    {
        using IServiceScope scope = CreateScope();
        await work(scope.ServiceProvider.GetRequiredService<OutboxDbContext>());
    }

    internal async Task RaiseShippedAsync() => await InScopeAsync(async context =>
    {
        Order order = new(Id<Customer>.New(), 500);
        context.Orders.Add(order);
        order.Ship();
        await context.SaveChangesAsync();
    });

    internal async Task<int> CountOwedAsync()
    {
        int owed = 0;
        await InScopeAsync(async context => owed = await context
            .Set<OutboxMessage>()
            .CountAsync(message => message.DeliveredAt == null && !message.Abandoned));

        return owed;
    }
}

internal sealed class OutboxDbContext(DbContextOptions<OutboxDbContext> options) : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();

    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder) =>
        configurationBuilder.UseLoomIdentities(typeof(Order).Assembly);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Order>(order =>
        {
            order.HasKey(o => o.Id);
            order.HasMany(o => o.Lines).WithOne().HasForeignKey(line => line.OrderId);
        });

        modelBuilder.Entity<OrderLine>().HasKey(line => line.Id);
        modelBuilder.Entity<AuditEntry>().HasKey(audit => audit.Id);

        modelBuilder.AddLoomOutbox();
    }
}
