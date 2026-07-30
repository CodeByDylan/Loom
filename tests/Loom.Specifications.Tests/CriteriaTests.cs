using System.Linq.Expressions;

namespace Loom.Specifications.Tests;

public class CriteriaTests
{
    private static readonly Expression<Func<Order, bool>> BigOrder = order => order.Total > 100;
    private static readonly Expression<Func<Order, bool>> AcmeOrder = order => order.Customer == "Acme";

    [Test]
    public async Task And_Requires_Both()
    {
        Func<Order, bool> predicate = BigOrder.And(AcmeOrder).Compile();

        await Assert.That(predicate(Order("Acme", 200))).IsTrue();
        await Assert.That(predicate(Order("Acme", 50))).IsFalse();
        await Assert.That(predicate(Order("Other", 200))).IsFalse();
    }

    [Test]
    public async Task Or_Requires_Either()
    {
        Func<Order, bool> predicate = BigOrder.Or(AcmeOrder).Compile();

        await Assert.That(predicate(Order("Other", 200))).IsTrue();
        await Assert.That(predicate(Order("Acme", 50))).IsTrue();
        await Assert.That(predicate(Order("Other", 50))).IsFalse();
    }

    [Test]
    public async Task Not_Inverts()
    {
        Func<Order, bool> predicate = BigOrder.Not().Compile();

        await Assert.That(predicate(Order("Acme", 50))).IsTrue();
        await Assert.That(predicate(Order("Acme", 200))).IsFalse();
    }

    [Test]
    public async Task Combining_Produces_A_Single_Parameter()
    {
        Expression<Func<Order, bool>> combined = BigOrder.And(AcmeOrder);

        // The two source lambdas had distinct parameter instances. If the right body were not
        // rewritten onto the left's parameter, the tree would reference a parameter that the lambda
        // does not declare, and a query provider would fail to translate it.
        await Assert.That(combined.Parameters.Count).IsEqualTo(1);

        ParameterCounter counter = new();
        counter.Visit(combined.Body);
        await Assert.That(counter.Distinct.Count).IsEqualTo(1);
        await Assert.That(counter.Distinct.Single()).IsEqualTo(combined.Parameters[0]);
    }

    [Test]
    public async Task Combining_Does_Not_Wrap_In_An_Invocation()
    {
        Expression<Func<Order, bool>> combined = BigOrder.And(AcmeOrder);

        // Expression.Invoke would be a shorter implementation and is a known cause of
        // query-translation failures, so the tree must be free of invocation nodes.
        InvocationFinder finder = new();
        finder.Visit(combined.Body);
        await Assert.That(finder.Found).IsFalse();
    }

    [Test]
    public async Task Combined_Predicates_Compose_Further()
    {
        Expression<Func<Order, bool>> combined = BigOrder.And(AcmeOrder).Or(order => order.Total == 1);

        Func<Order, bool> predicate = combined.Compile();

        await Assert.That(predicate(Order("Nobody", 1))).IsTrue();
        await Assert.That(predicate(Order("Acme", 200))).IsTrue();
        await Assert.That(predicate(Order("Nobody", 2))).IsFalse();
    }

    [Test]
    public async Task Null_Arguments_Are_Rejected()
    {
        await Assert.That(() => BigOrder.And(null!)).Throws<ArgumentNullException>();
        await Assert.That(() => BigOrder.Or(null!)).Throws<ArgumentNullException>();
        await Assert.That(() => ((Expression<Func<Order, bool>>)null!).Not()).Throws<ArgumentNullException>();
    }

    private static Order Order(string customer, int total) =>
        new(customer, total, new DateOnly(2026, 1, 1));

    private sealed class ParameterCounter : ExpressionVisitor
    {
        internal HashSet<ParameterExpression> Distinct { get; } = [];

        protected override Expression VisitParameter(ParameterExpression node)
        {
            Distinct.Add(node);
            return base.VisitParameter(node);
        }
    }

    private sealed class InvocationFinder : ExpressionVisitor
    {
        internal bool Found { get; private set; }

        protected override Expression VisitInvocation(InvocationExpression node)
        {
            Found = true;
            return base.VisitInvocation(node);
        }
    }
}
