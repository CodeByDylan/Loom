using Loom.Entities;

namespace Ordering.Domain.Customers;

/// <summary>
/// Someone who places orders. An aggregate of its own, referenced by orders only through its identity.
/// </summary>
public sealed class Customer : AggregateRoot<Customer>
{
    /// <summary>
    /// Creates a customer, minting its identity.
    /// </summary>
    /// <param name="name">What to call them. Required.</param>
    /// <exception cref="ArgumentException"><paramref name="name" /> is missing or blank.</exception>
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

    /// <summary>Gets what to call them. Set on construction and never blank.</summary>
    public string Name { get; private set; } = string.Empty;
}
