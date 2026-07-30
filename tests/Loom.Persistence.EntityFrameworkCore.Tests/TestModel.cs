using Loom.Entities;
using Loom.Specifications;
using Microsoft.EntityFrameworkCore;

namespace Loom.Persistence.EntityFrameworkCore.Tests;

internal sealed class Customer : Entity<Customer>
{
    public Customer(string name) => Name = name;

    // EF materialises through this: it writes the identity from the column, so a parameterless
    // constructor is unambiguous no matter what the domain constructors look like.
    private Customer()
    {
    }

    public string Name { get; private set; } = string.Empty;
}

internal sealed class Order : AggregateRoot<Order>
{
    public Order(Id<Customer> customerId, int total)
    {
        CustomerId = customerId;
        Total = total;
    }

    private Order()
    {
    }

    public Id<Customer> CustomerId { get; private set; }

    public int Total { get; private set; }

    public bool IsCancelled { get; private set; }

    // A related collection rather than an owned one, so that eager loading is actually observable:
    // an owned collection loads automatically and Include over it would prove nothing.
    public List<OrderLine> Lines { get; } = [];

    public void AddLine(string sku) => Lines.Add(new OrderLine(Id, sku));

    public void Cancel()
    {
        IsCancelled = true;
        Raise(new OrderCancelled(Id, Total));
    }

    public void Touch() => Raise(new OrderTouched(Id));

    public void Cascade(int remaining) => Raise(new OrderCascaded(Id, remaining));

    public void Ship() => Raise(new OrderShipped(Id));
}

internal sealed class OrderLine : Entity<OrderLine>
{
    public OrderLine(Id<Order> orderId, string sku)
    {
        OrderId = orderId;
        Sku = sku;
    }

    private OrderLine()
    {
    }

    public Id<Order> OrderId { get; private set; }

    public string Sku { get; private set; } = string.Empty;
}

internal sealed record OrderCancelled(Id<Order> OrderId, int Total) : IDomainEvent;

internal sealed record OrderTouched(Id<Order> OrderId) : IDomainEvent;

/// <summary>A link in a chain of a known length, for approaching the drain limit from both sides.</summary>
internal sealed record OrderCascaded(Id<Order> OrderId, int Remaining) : IDomainEvent;

internal sealed record OrderShipped(Id<Order> OrderId) : IDeferredDomainEvent;

/// <summary>Written by an event handler, to prove a handler's changes join the same transaction.</summary>
internal sealed class AuditEntry
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    public string Note { get; init; } = string.Empty;
}

internal sealed class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();

    public DbSet<OrderLine> OrderLines => Set<OrderLine>();

    public DbSet<Customer> Customers => Set<Customer>();

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
        modelBuilder.Entity<Customer>().HasKey(customer => customer.Id);
        modelBuilder.Entity<AuditEntry>().HasKey(audit => audit.Id);
    }
}

internal sealed class LargeOrders : Specification<Order>
{
    public LargeOrders(int amount) => Where(order => order.Total > amount);
}

internal sealed class LargeOrdersWithLines : Specification<Order>
{
    public LargeOrdersWithLines(int amount)
    {
        Where(order => order.Total > amount);
        Include(order => order.Lines);
        OrderByDescending(order => order.Total);
    }
}

internal sealed class OrdersSmallestFirst : Specification<Order>
{
    public OrdersSmallestFirst() => OrderBy(order => order.Total);
}
