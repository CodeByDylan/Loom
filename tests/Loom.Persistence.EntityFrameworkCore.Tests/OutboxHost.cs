using Loom.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Loom.Persistence.EntityFrameworkCore.Tests;

/// <summary>
/// A host with the outbox configured, driving delivery by hand rather than on a timer.
/// </summary>
internal sealed class OutboxHost : IAsyncDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _provider;

    private OutboxHost(SqliteConnection connection, ServiceProvider provider)
    {
        _connection = connection;
        _provider = provider;
    }

    internal static async Task<OutboxHost> CreateAsync(bool failing = false, int maximumAttempts = 5)
    {
        SqliteConnection connection = new("Filename=:memory:");
        await connection.OpenAsync();

        ServiceCollection services = new();
        services.AddSingleton<Recorder>();

        services.AddLoomPersistence(options => options.UseOutbox<OutboxDbContext>(outbox =>
        {
            outbox.MaximumAttempts = maximumAttempts;
            outbox.EventAssemblies = [typeof(OrderShipped).Assembly];
        }));

        services.AddDbContext<OutboxDbContext>((serviceProvider, options) => options
            .UseSqlite(connection)
            .AddInterceptors(serviceProvider.GetRequiredService<DomainEventInterceptor>()));

        services.AddScoped<IDomainEventHandler<OrderCancelled>, OutboxTests.CancelledHandler>();

        if (failing)
        {
            services.AddScoped<IDomainEventHandler<OrderShipped>, OutboxTests.FailingShippedHandler>();
        }
        else
        {
            services.AddScoped<IDomainEventHandler<OrderShipped>, OutboxTests.ShippedHandler>();
        }

        ServiceProvider provider = services.BuildServiceProvider();

        using (IServiceScope scope = provider.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<OutboxDbContext>().Database.EnsureCreatedAsync();
        }

        return new OutboxHost(connection, provider);
    }

    internal Recorder Recorder => _provider.GetRequiredService<Recorder>();

    internal OutboxProcessor<OutboxDbContext> Processor =>
        _provider.GetRequiredService<OutboxProcessor<OutboxDbContext>>();

    internal async Task InScopeAsync(Func<OutboxDbContext, Task> work)
    {
        using IServiceScope scope = _provider.CreateScope();
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

    public async ValueTask DisposeAsync()
    {
        await _provider.DisposeAsync();
        await _connection.DisposeAsync();
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
