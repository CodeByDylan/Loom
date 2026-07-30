using Loom.Entities;

namespace Ordering.Domain.Customers;

public sealed class Customer : AggregateRoot<Customer>
{
    public Customer(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
    }

    // Entity Framework materialises through this. It writes the identity from the column, so the one
    // minted by the base constructor is discarded.
    private Customer()
    {
    }

    public string Name { get; private set; } = string.Empty;
}
