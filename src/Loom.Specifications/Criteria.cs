using System.Linq.Expressions;

namespace Loom.Specifications;

/// <summary>
/// Combines predicate expressions.
/// </summary>
/// <remarks>
/// Composition operates on criteria rather than on whole specifications. Two specifications each
/// carrying their own eager-loading and ordering have no well-defined combination — whose ordering
/// wins? — so the composable part is the predicate, and a specification that needs a combined
/// predicate builds one here and passes it to <c>Where</c>.
/// </remarks>
public static class Criteria
{
    /// <summary>
    /// Requires both predicates to hold.
    /// </summary>
    /// <typeparam name="T">The type being tested.</typeparam>
    /// <param name="left">The first predicate.</param>
    /// <param name="right">The second predicate.</param>
    /// <returns>A predicate that holds when both hold.</returns>
    /// <exception cref="ArgumentNullException">Either predicate is <see langword="null" />.</exception>
    public static Expression<Func<T, bool>> And<T>(
        this Expression<Func<T, bool>> left,
        Expression<Func<T, bool>> right) => Combine(left, right, Expression.AndAlso);

    /// <summary>
    /// Requires either predicate to hold.
    /// </summary>
    /// <typeparam name="T">The type being tested.</typeparam>
    /// <param name="left">The first predicate.</param>
    /// <param name="right">The second predicate.</param>
    /// <returns>A predicate that holds when either holds.</returns>
    /// <exception cref="ArgumentNullException">Either predicate is <see langword="null" />.</exception>
    public static Expression<Func<T, bool>> Or<T>(
        this Expression<Func<T, bool>> left,
        Expression<Func<T, bool>> right) => Combine(left, right, Expression.OrElse);

    /// <summary>
    /// Inverts a predicate.
    /// </summary>
    /// <typeparam name="T">The type being tested.</typeparam>
    /// <param name="predicate">The predicate to invert.</param>
    /// <returns>A predicate that holds when <paramref name="predicate" /> does not.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="predicate" /> is <see langword="null" />.</exception>
    public static Expression<Func<T, bool>> Not<T>(this Expression<Func<T, bool>> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        return Expression.Lambda<Func<T, bool>>(
            Expression.Not(predicate.Body),
            predicate.Parameters[0]);
    }

    private static Expression<Func<T, bool>> Combine<T>(
        Expression<Func<T, bool>> left,
        Expression<Func<T, bool>> right,
        Func<Expression, Expression, BinaryExpression> combine)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        // The two lambdas have distinct parameter instances, so the right body must be rewritten to
        // use the left's parameter before the bodies can be joined. Wrapping in Expression.Invoke
        // would be shorter and is a known source of query-translation failures, so it is rewritten
        // properly instead.
        ParameterExpression parameter = left.Parameters[0];
        Expression rebound = new ParameterRebinder(right.Parameters[0], parameter).Visit(right.Body);

        return Expression.Lambda<Func<T, bool>>(combine(left.Body, rebound), parameter);
    }

    private sealed class ParameterRebinder(ParameterExpression replace, Expression with) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) =>
            node == replace ? with : base.VisitParameter(node);
    }
}
