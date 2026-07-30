using Loom.Entities;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Loom.Persistence;

/// <summary>
/// Stores an <see cref="Id{TEntity}" /> as its underlying value.
/// </summary>
/// <typeparam name="TEntity">The entity the identity belongs to.</typeparam>
public sealed class IdValueConverter<TEntity>()
    : ValueConverter<Id<TEntity>, Guid>(id => id.Value, value => Id<TEntity>.From(value));
