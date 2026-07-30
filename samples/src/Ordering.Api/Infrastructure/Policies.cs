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
    public const string OrdersRead = "orders:read";

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
