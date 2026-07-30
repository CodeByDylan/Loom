using System.Security.Claims;
using Loom.Entities;
using Microsoft.AspNetCore.Authorization;
using Ordering.Domain.Customers;

namespace Ordering.Api.Infrastructure;

/// <summary>
/// Authorization policy names, as constants. Never an inline string at a call site.
/// </summary>
public static class Policies
{
    /// <summary>Required to look at orders. Satisfied by any caller who identifies a customer.</summary>
    public const string OrdersRead = "orders:read";

    /// <summary>
    /// Required to place, cancel or ship an order. Whether the order is <em>theirs</em> is a separate
    /// question the handler answers, because it needs the order.
    /// </summary>
    public const string OrdersWrite = "orders:write";
}

/// <summary>
/// The customer the request is acting as.
/// </summary>
/// <remarks>
/// Whether the caller is authenticated is the pipeline's business. Whether a given order belongs to
/// them needs the order, so it cannot be an attribute or a policy — that check lives in the handler
/// and reports <c>Forbidden</c>.
/// </remarks>
public interface ICurrentCustomer
{
    /// <summary>
    /// Gets the customer named by the request's claim.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The request carries no usable claim. Only reachable from an endpoint that forgot to require
    /// authorization, so it is a bug rather than a refusal.
    /// </exception>
    Id<Customer> Id { get; }
}

internal sealed class CurrentCustomer(IHttpContextAccessor accessor) : ICurrentCustomer
{
    public const string ClaimType = "customer_id";

    /// <summary>
    /// Whether the caller carries a claim that names a customer.
    /// </summary>
    /// <remarks>
    /// Used by the policies, so a malformed claim is refused by the pipeline rather than reaching
    /// <see cref="Id" /> and throwing. Requiring only that the claim exists would turn a request that
    /// should be refused into a server error.
    /// </remarks>
    public static bool IsIdentifiable(AuthorizationHandlerContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return Id<Customer>.TryParse(
            context.User.FindFirstValue(ClaimType),
            provider: null,
            out _);
    }

    public Id<Customer> Id
    {
        get
        {
            string? value = accessor.HttpContext?.User.FindFirstValue(ClaimType);

            return Id<Customer>.TryParse(value, provider: null, out Id<Customer> id)
                ? id
                : throw new InvalidOperationException(
                    $"The request has no usable '{ClaimType}' claim. An endpoint reaching this should "
                    + "have required authorization.");
        }
    }
}
