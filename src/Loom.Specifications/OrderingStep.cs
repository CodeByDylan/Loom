using System.Linq.Expressions;

namespace Loom.Specifications;

/// <summary>
/// A named business rule over <typeparamref name="T" />: which items match, what to load with them,
/// and in what order.
/// </summary>
/// <typeparam name="T">The type being described.</typeparam>
/// <remarks>
/// A specification is applied to a queryable that the caller still owns — it is never handed to a
/// repository. The slice keeps its own query visible and merely applies a reusable rule to it.
/// <para>
/// Paging is deliberately absent. A named rule such as "overdue orders" is reusable; "page two of
/// twenty" is a caller's decision, and putting it here would mean constructing a new specification
/// for every page.
/// </para>
/// </remarks>
public interface ISpecification<T>
{
    /// <summary>
    /// Gets the predicate items must satisfy, or <see langword="null" /> to match everything.
    /// </summary>
    Expression<Func<T, bool>>? Criteria { get; }

    /// <summary>
    /// Gets the related data to load alongside each item, one level deep.
    /// </summary>
    /// <remarks>Applying these requires an object-relational mapper; see <see cref="ISpecification{T}" />.</remarks>
    IReadOnlyList<Expression<Func<T, object?>>> Includes { get; }

    /// <summary>
    /// Gets dot-separated paths of related data to load, for nesting deeper than one level.
    /// </summary>
    /// <remarks>
    /// Strings rather than expressions, because chaining nested eager loads requires types belonging
    /// to a specific object-relational mapper, which would drag this package down a tier. Not
    /// refactor-safe; prefer <see cref="Includes" /> where one level suffices.
    /// </remarks>
    IReadOnlyList<string> IncludePaths { get; }

    /// <summary>
    /// Gets the ordering to apply, in order of precedence.
    /// </summary>
    IReadOnlyList<OrderingStep<T>> Ordering { get; }
}

/// <summary>
/// One level of ordering.
/// </summary>
/// <typeparam name="T">The type being ordered.</typeparam>
/// <param name="KeySelector">Selects the value to order by.</param>
/// <param name="Descending">Whether to order from largest to smallest.</param>
public sealed record OrderingStep<T>(Expression<Func<T, object?>> KeySelector, bool Descending);
