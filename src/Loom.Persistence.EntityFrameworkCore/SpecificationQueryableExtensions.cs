using Loom.Specifications;
using Microsoft.EntityFrameworkCore;

namespace Loom.Persistence;

/// <summary>
/// Applies specifications to Entity Framework Core queries.
/// </summary>
public static class SpecificationQueryableExtensions
{
    /// <summary>
    /// Applies a specification in full: eager loading, criteria and ordering.
    /// </summary>
    /// <typeparam name="T">The entity being queried.</typeparam>
    /// <param name="source">The query to apply to. Still owned by the caller.</param>
    /// <param name="specification">The rule to apply.</param>
    /// <returns>The query with the specification applied.</returns>
    /// <exception cref="ArgumentNullException">Either argument is <see langword="null" />.</exception>
    /// <remarks>
    /// Named differently from the specification package's own <c>Apply</c> on purpose. That overload
    /// is unconstrained and this one requires a reference type, so both would be applicable to the
    /// same call and a file importing both namespaces would fail to compile with an ambiguity that
    /// is tedious to diagnose.
    /// <para>
    /// Eager loading is applied first, so that ordering and filtering compose over the loaded graph
    /// the way they would if written by hand.
    /// </para>
    /// </remarks>
    public static IQueryable<T> ApplySpecification<T>(this IQueryable<T> source, ISpecification<T> specification)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(specification);

        IQueryable<T> query = source;

        foreach (System.Linq.Expressions.Expression<Func<T, object?>> include in specification.Includes)
        {
            query = query.Include(include!);
        }

        // Nested loading arrives as a path rather than an expression, because chaining typed
        // eager loads needs types from this package and the specification must stay free of it.
        foreach (string path in specification.IncludePaths)
        {
            query = query.Include(path);
        }

        return query.ApplyCriteriaAndOrdering(specification);
    }
}
