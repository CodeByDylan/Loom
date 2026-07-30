using Loom.Results;

namespace Ordering.Domain.Orders;

/// <summary>
/// The failures an order can report. Codes are stable and greppable; messages are free to be reworded.
/// </summary>
public static class OrderErrors
{
    /// <summary>An order was placed with nothing on it.</summary>
    public static Error NoLines { get; } =
        Errors.Invalid("orders.no_lines", "An order must have at least one line.");

    /// <summary>A line is missing its stock code, or asks for nothing.</summary>
    public static Error InvalidLine { get; } =
        Errors.Invalid("orders.invalid_line", "Every line needs a stock code and a positive amount.");

    /// <summary>The order was already cancelled, so cancelling or shipping it again means nothing.</summary>
    public static Error AlreadyCancelled { get; } =
        Errors.Conflict("orders.already_cancelled", "The order is already cancelled.");

    /// <summary>The order has shipped, which is the point after which it can no longer be called off.</summary>
    public static Error AlreadyShipped { get; } =
        Errors.Conflict("orders.already_shipped", "The order has already shipped.");

    /// <summary>No order exists under that identifier.</summary>
    public static Error NotFound { get; } =
        Errors.NotFound("orders.not_found", "No such order.");

    /// <summary>
    /// The order exists but belongs to another customer. Distinct from <see cref="NotFound" /> on
    /// purpose, since the caller is authenticated and the ownership check is the handler's to make.
    /// </summary>
    public static Error NotYours { get; } =
        Errors.Forbidden("orders.not_yours", "That order belongs to another customer.");
}
