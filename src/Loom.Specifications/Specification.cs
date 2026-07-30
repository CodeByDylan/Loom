using System.Linq.Expressions;

namespace Loom.Specifications;

/// <summary>
/// Base class for a named business rule. Derive from it and configure the rule in the constructor.
/// </summary>
/// <typeparam name="T">The type being described.</typeparam>
/// <remarks>
/// A specification is a <em>named rule</em>, not a parameter bag. <c>OverdueOrders(customerId)</c> is
/// the intended shape. A specification that grows a boolean toggling part of the query is two
/// specifications wearing one name — split it.
/// <para>
/// Not sealed: deriving from it is the point. A documented exception to the seal-by-default rule.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// internal sealed class OverdueOrders : Specification&lt;Order&gt;
/// {
///     public OverdueOrders(Guid customerId, DateOnly asOf)
///     {
///         Where(order => order.CustomerId == customerId &amp;&amp; order.DueOn &lt; asOf);
///         Include(order => order.Items);
///         OrderBy(order => order.DueOn);
///     }
/// }
/// </code>
/// </example>
public abstract class Specification<T> : ISpecification<T>
{
    private readonly List<Expression<Func<T, object?>>> _includes = [];
    private readonly List<string> _includePaths = [];
    private readonly List<OrderingStep<T>> _ordering = [];
    private Func<T, bool>? _compiled;

    /// <inheritdoc />
    public Expression<Func<T, bool>>? Criteria { get; private set; }

    /// <inheritdoc />
    public IReadOnlyList<Expression<Func<T, object?>>> Includes => _includes;

    /// <inheritdoc />
    public IReadOnlyList<string> IncludePaths => _includePaths;

    /// <inheritdoc />
    public IReadOnlyList<OrderingStep<T>> Ordering => _ordering;

    /// <summary>
    /// Tests one item against this specification's criteria, in memory.
    /// </summary>
    /// <param name="item">The item to test.</param>
    /// <returns><see langword="true" /> if the item matches, or if there are no criteria.</returns>
    /// <remarks>
    /// The compiled predicate is cached, so repeated calls do not recompile. Eager loading and
    /// ordering are irrelevant here and ignored.
    /// </remarks>
    public bool IsSatisfiedBy(T item) => CompiledCriteria is null || CompiledCriteria(item);

    private Func<T, bool>? CompiledCriteria => Criteria is null
        ? null
        : _compiled ??= Criteria.Compile();

    /// <summary>
    /// Sets the predicate items must satisfy. Calling this again replaces the previous predicate;
    /// combine with <see cref="Criteria" /> helpers to require several conditions.
    /// </summary>
    /// <param name="criteria">The predicate.</param>
    /// <exception cref="ArgumentNullException"><paramref name="criteria" /> is <see langword="null" />.</exception>
    protected void Where(Expression<Func<T, bool>> criteria)
    {
        ArgumentNullException.ThrowIfNull(criteria);
        Criteria = criteria;
        _compiled = null;
    }

    /// <summary>
    /// Loads related data alongside each item.
    /// </summary>
    /// <param name="include">Selects the related data.</param>
    /// <exception cref="ArgumentNullException"><paramref name="include" /> is <see langword="null" />.</exception>
    protected void Include(Expression<Func<T, object?>> include)
    {
        ArgumentNullException.ThrowIfNull(include);
        _includes.Add(include);
    }

    /// <summary>
    /// Loads related data nested more than one level deep.
    /// </summary>
    /// <param name="path">A dot-separated path, such as <c>Items.Product</c>.</param>
    /// <exception cref="ArgumentException"><paramref name="path" /> is null, empty, or whitespace.</exception>
    protected void Include(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _includePaths.Add(path);
    }

    /// <summary>Orders results by a value, smallest first. Replaces any existing ordering.</summary>
    /// <param name="keySelector">Selects the value to order by.</param>
    /// <exception cref="ArgumentNullException"><paramref name="keySelector" /> is <see langword="null" />.</exception>
    protected void OrderBy(Expression<Func<T, object?>> keySelector) => SetOrdering(keySelector, descending: false);

    /// <summary>Orders results by a value, largest first. Replaces any existing ordering.</summary>
    /// <param name="keySelector">Selects the value to order by.</param>
    /// <exception cref="ArgumentNullException"><paramref name="keySelector" /> is <see langword="null" />.</exception>
    protected void OrderByDescending(Expression<Func<T, object?>> keySelector) =>
        SetOrdering(keySelector, descending: true);

    /// <summary>Breaks ties in the existing ordering, smallest first.</summary>
    /// <param name="keySelector">Selects the value to order by.</param>
    /// <exception cref="ArgumentNullException"><paramref name="keySelector" /> is <see langword="null" />.</exception>
    /// <exception cref="InvalidOperationException">No ordering has been set yet.</exception>
    protected void ThenBy(Expression<Func<T, object?>> keySelector) => AddOrdering(keySelector, descending: false);

    /// <summary>Breaks ties in the existing ordering, largest first.</summary>
    /// <param name="keySelector">Selects the value to order by.</param>
    /// <exception cref="ArgumentNullException"><paramref name="keySelector" /> is <see langword="null" />.</exception>
    /// <exception cref="InvalidOperationException">No ordering has been set yet.</exception>
    protected void ThenByDescending(Expression<Func<T, object?>> keySelector) =>
        AddOrdering(keySelector, descending: true);

    private void SetOrdering(Expression<Func<T, object?>> keySelector, bool descending)
    {
        ArgumentNullException.ThrowIfNull(keySelector);
        _ordering.Clear();
        _ordering.Add(new OrderingStep<T>(keySelector, descending));
    }

    private void AddOrdering(Expression<Func<T, object?>> keySelector, bool descending)
    {
        ArgumentNullException.ThrowIfNull(keySelector);

        if (_ordering.Count is 0)
        {
            throw new InvalidOperationException(
                "Call OrderBy or OrderByDescending before ThenBy or ThenByDescending.");
        }

        _ordering.Add(new OrderingStep<T>(keySelector, descending));
    }
}
