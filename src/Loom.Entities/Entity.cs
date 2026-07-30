namespace Loom.Entities;

/// <summary>
/// A domain entity: something with an identity that persists as its state changes.
/// </summary>
/// <typeparam name="TSelf">The deriving entity type.</typeparam>
/// <remarks>
/// Entities are compared by identity, never by their contents. Two <c>Order</c> objects with the
/// same <see cref="Id" /> are the same order even if their other fields have diverged — which is
/// exactly why an entity must be a <see langword="class" /> and never a <see langword="record" />.
/// A record would give structural equality, the opposite of what identity requires.
/// <para>
/// This type is deliberately not sealed: deriving from it is the point. It is a documented exception
/// to the seal-by-default rule.
/// </para>
/// <para>
/// The <typeparamref name="TSelf" /> parameter also solves a real problem with object-relational
/// mappers. Comparing <c>GetType()</c> would make a lazy-loading proxy unequal to a plain instance
/// with the same identity; because equality is defined against <c>Entity&lt;TSelf&gt;</c>, a proxy
/// of <c>Order</c> is still an <c>Entity&lt;Order&gt;</c> and compares correctly.
/// </para>
/// </remarks>
public abstract class Entity<TSelf> : IEquatable<Entity<TSelf>>
    where TSelf : Entity<TSelf>
{
    /// <summary>
    /// Creates an entity with a new identity.
    /// </summary>
    protected Entity() => Id = Id<TSelf>.New();

    /// <summary>
    /// Reconstructs an entity with a known identity.
    /// </summary>
    /// <param name="id">The stored identity.</param>
    /// <exception cref="ArgumentException"><paramref name="id" /> is the default value.</exception>
    /// <remarks>
    /// This is the constructor a persistence layer must use when materializing a stored entity.
    /// Routing materialization through the parameterless constructor would mint a fresh identity and
    /// silently detach the object from its stored row.
    /// </remarks>
    protected Entity(Id<TSelf> id)
    {
        if (id == default)
        {
            throw new ArgumentException(
                "An entity cannot be constructed with a default identity. Use Id<T>.From to "
                + "reconstruct a stored identity, or the parameterless constructor for a new entity.",
                nameof(id));
        }

        Id = id;
    }

    /// <summary>
    /// Gets this entity's identity, which never changes and is never the default value.
    /// </summary>
    public Id<TSelf> Id { get; }

    /// <inheritdoc />
    public bool Equals(Entity<TSelf>? other) => other is not null && Id == other.Id;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Entity<TSelf> other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => Id.GetHashCode();

    /// <summary>Compares two entities by identity.</summary>
    /// <param name="left">The first entity.</param>
    /// <param name="right">The second entity.</param>
    /// <returns><see langword="true" /> if they share an identity, or are both <see langword="null" />.</returns>
    public static bool operator ==(Entity<TSelf>? left, Entity<TSelf>? right) =>
        left is null ? right is null : left.Equals(right);

    /// <summary>Compares two entities by identity.</summary>
    /// <param name="left">The first entity.</param>
    /// <param name="right">The second entity.</param>
    /// <returns><see langword="true" /> if they do not share an identity.</returns>
    public static bool operator !=(Entity<TSelf>? left, Entity<TSelf>? right) => !(left == right);

    /// <inheritdoc />
    public override string ToString() => $"{typeof(TSelf).Name} {Id}";
}
