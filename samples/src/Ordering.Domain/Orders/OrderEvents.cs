using Loom.Entities;
using Ordering.Domain.Customers;

namespace Ordering.Domain.Orders;

/// <summary>
/// Dispatched before the transaction commits, so a handler may change data atomically with the
/// cancellation — but must not reach outside the process.
/// </summary>
public sealed record OrderCancelled(Id<Order> OrderId, int Total) : IDomainEvent;

/// <summary>
/// Dispatched after the transaction commits, at least once, so a handler may notify the customer.
/// A handler must therefore be idempotent.
/// </summary>
public sealed record OrderShipped(Id<Order> OrderId, Id<Customer> CustomerId) : IDeferredDomainEvent;
