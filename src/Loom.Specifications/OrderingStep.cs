using System.Linq.Expressions;

namespace Loom.Specifications;

/// <summary>
/// One level of ordering.
/// </summary>
/// <typeparam name="T">The type being ordered.</typeparam>
/// <param name="KeySelector">Selects the value to order by.</param>
/// <param name="Descending">Whether to order from largest to smallest.</param>
public sealed record OrderingStep<T>(Expression<Func<T, object?>> KeySelector, bool Descending);
