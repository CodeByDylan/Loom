using System.Linq.Expressions;

namespace Loom.Specifications;

/// <summary>
/// Applies specifications to sequences.
/// </summary>
public static class SpecificationExtensions
{
    /// <summary>
    /// Applies a specification's criteria and ordering to a query.
    /// </summary>
    /// <typeparam name="T">The type being queried.</typeparam>
    /// <param name="source">The query to filter and order. Still owned by the caller.</param>
    /// <param name="specification">The rule to apply.</param>
    /// <returns>The filtered, ordered query.</returns>
    /// <exception cref="ArgumentNullException">Either argument is <see langword="null" />.</exception>
    /// <exception cref="NotSupportedException">
    /// The specification declares eager loading, which cannot be honoured without an
    /// object-relational mapper. Use the Loom persistence package's overload instead.
    /// </exception>
    /// <remarks>
    /// Fails loudly rather than silently skipping eager loading. Quietly ignoring it would leave a
    /// caller with unloaded relationships and no indication why.
    /// </remarks>
    public static IQueryable<T> Apply<T>(this IQueryable<T> source, ISpecification<T> specification)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(specification);

        if (specification.Includes.Count > 0 || specification.IncludePaths.Count > 0)
        {
            throw new NotSupportedException(
                $"'{specification.GetType().Name}' declares eager loading, which this package cannot "
                + "apply. Apply it with the Loom persistence package for your object-relational "
                + "mapper, or remove the Include calls from the specification.");
        }

        return source.ApplyCriteriaAndOrdering(specification);
    }

    /// <summary>
    /// Applies a specification's criteria and ordering to an in-memory sequence.
    /// </summary>
    /// <typeparam name="T">The type being filtered.</typeparam>
    /// <param name="source">The sequence to filter and order.</param>
    /// <param name="specification">The rule to apply.</param>
    /// <returns>The filtered, ordered sequence.</returns>
    /// <exception cref="ArgumentNullException">Either argument is <see langword="null" />.</exception>
    /// <remarks>
    /// Eager loading is ignored rather than rejected here: an in-memory object graph is already
    /// loaded, so there is nothing to eagerly load.
    /// </remarks>
    public static IEnumerable<T> Apply<T>(this IEnumerable<T> source, ISpecification<T> specification)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(specification);

        IEnumerable<T> result = specification.Criteria is null
            ? source
            : source.Where(specification.Criteria.Compile());

        if (specification.Ordering.Count is 0)
        {
            return result;
        }

        OrderingStep<T> first = specification.Ordering[0];
        Func<T, object?> firstKey = first.KeySelector.Compile();

        IOrderedEnumerable<T> ordered = first.Descending
            ? result.OrderByDescending(firstKey)
            : result.OrderBy(firstKey);

        foreach (OrderingStep<T> step in specification.Ordering.Skip(1))
        {
            Func<T, object?> key = step.KeySelector.Compile();
            ordered = step.Descending ? ordered.ThenByDescending(key) : ordered.ThenBy(key);
        }

        return ordered;
    }

    /// <summary>
    /// Applies a specification's criteria and ordering to a query, ignoring eager loading.
    /// </summary>
    /// <typeparam name="T">The type being queried.</typeparam>
    /// <param name="source">The query to filter and order.</param>
    /// <param name="specification">The rule to apply.</param>
    /// <returns>The filtered, ordered query.</returns>
    /// <exception cref="ArgumentNullException">Either argument is <see langword="null" />.</exception>
    /// <remarks>
    /// For a persistence package that has already applied eager loading itself and needs the rest of
    /// the specification applied. Consumers should call <see cref="Apply{T}(IQueryable{T}, ISpecification{T})" />.
    /// </remarks>
    public static IQueryable<T> ApplyCriteriaAndOrdering<T>(
        this IQueryable<T> source,
        ISpecification<T> specification)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(specification);

        IQueryable<T> result = specification.Criteria is null
            ? source
            : source.Where(specification.Criteria);

        if (specification.Ordering.Count is 0)
        {
            return result;
        }

        OrderingStep<T> first = specification.Ordering[0];
        IOrderedQueryable<T> ordered = first.Descending
            ? result.OrderByDescending(first.KeySelector)
            : result.OrderBy(first.KeySelector);

        foreach (OrderingStep<T> step in specification.Ordering.Skip(1))
        {
            ordered = step.Descending
                ? ordered.ThenByDescending(step.KeySelector)
                : ordered.ThenBy(step.KeySelector);
        }

        return ordered;
    }
}
