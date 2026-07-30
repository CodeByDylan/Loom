using Loom.Results;

namespace Ordering.Domain.Orders;

/// <summary>
/// The failures an order can report. Codes are stable and greppable; messages are free to be reworded.
/// </summary>
public static class OrderErrors
{
    public static Error NoLines { get; } =
        Errors.Invalid("orders.no_lines", "An order must have at least one line.");

    public static Error AlreadyCancelled { get; } =
        Errors.Conflict("orders.already_cancelled", "The order is already cancelled.");

    public static Error AlreadyShipped { get; } =
        Errors.Conflict("orders.already_shipped", "The order has already shipped.");

    public static Error NotFound { get; } =
        Errors.NotFound("orders.not_found", "No such order.");

    public static Error NotYours { get; } =
        Errors.Forbidden("orders.not_yours", "That order belongs to another customer.");
}
