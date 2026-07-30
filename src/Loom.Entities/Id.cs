using System.Diagnostics;
using System.Text.Json.Serialization;

namespace Loom.Entities;

/// <summary>
/// The identity of a <typeparamref name="TEntity" />.
/// </summary>
/// <typeparam name="TEntity">The entity this identity belongs to.</typeparam>
/// <remarks>
/// One generic type serves every entity, and <c>Id&lt;Order&gt;</c> is not assignable to
/// <c>Id&lt;Customer&gt;</c>, so identities cannot be crossed by accident.
/// <para>
/// Values are UUID version 7, which embeds a millisecond timestamp. That keeps index locality good
/// on insert, unlike random version 4 values.
/// </para>
/// <para>
/// <strong>Ordering is approximate.</strong> .NET does not make version 7 values monotonic within a
/// single millisecond, so two identities created in quick succession sort arbitrarily — measured at
/// roughly a 50% inversion rate. <see cref="CompareTo(Id{TEntity})" /> is a stable total order
/// suitable for sorted collections and index locality, but it is <em>not</em> a creation-order key.
/// Do not paginate on it, and do not use it to establish which of two things happened first.
/// </para>
/// <para>
/// There is deliberately no <c>Empty</c> or <c>Default</c>: an entity always has an identity, so a
/// default-valued identity is invalid rather than a state worth naming.
/// </para>
/// </remarks>
[DebuggerDisplay("{Value}")]
[JsonConverter(typeof(IdJsonConverterFactory))]
public readonly struct Id<TEntity> :
    IEquatable<Id<TEntity>>,
    IComparable<Id<TEntity>>,
    IParsable<Id<TEntity>>
{
    private Id(Guid value) => Value = value;

    /// <summary>
    /// Gets the underlying value.
    /// </summary>
    public Guid Value { get; }

    /// <summary>
    /// Creates a new identity.
    /// </summary>
    /// <returns>A time-ordered identity that has never been used before.</returns>
    public static Id<TEntity> New() => new(Guid.CreateVersion7());

    /// <summary>
    /// Reconstructs an identity from a stored value.
    /// </summary>
    /// <param name="value">The stored value. Must not be empty.</param>
    /// <returns>The identity.</returns>
    /// <exception cref="ArgumentException"><paramref name="value" /> is <see cref="Guid.Empty" />.</exception>
    public static Id<TEntity> From(Guid value) => value == Guid.Empty
        ? throw new ArgumentException("An entity identity cannot be empty.", nameof(value))
        : new Id<TEntity>(value);

    /// <inheritdoc />
    public static Id<TEntity> Parse(string s, IFormatProvider? provider) => From(Guid.Parse(s, provider));

    /// <inheritdoc />
    public static bool TryParse(string? s, IFormatProvider? provider, out Id<TEntity> result)
    {
        if (Guid.TryParse(s, provider, out Guid value) && value != Guid.Empty)
        {
            result = new Id<TEntity>(value);
            return true;
        }

        result = default;
        return false;
    }

    /// <summary>
    /// Unwraps the underlying value. Explicit, so that an identity cannot be passed where a bare
    /// <see cref="Guid" /> is expected without saying so.
    /// </summary>
    /// <param name="id">The identity to unwrap.</param>
    public static explicit operator Guid(Id<TEntity> id) => id.Value;

    /// <inheritdoc />
    public bool Equals(Id<TEntity> other) => Value.Equals(other.Value);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Id<TEntity> other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => Value.GetHashCode();

    /// <inheritdoc />
    public int CompareTo(Id<TEntity> other) => Value.CompareTo(other.Value);

    /// <summary>Compares two identities for equality.</summary>
    /// <param name="left">The first identity.</param>
    /// <param name="right">The second identity.</param>
    /// <returns><see langword="true" /> if they are equal.</returns>
    public static bool operator ==(Id<TEntity> left, Id<TEntity> right) => left.Equals(right);

    /// <summary>Compares two identities for inequality.</summary>
    /// <param name="left">The first identity.</param>
    /// <param name="right">The second identity.</param>
    /// <returns><see langword="true" /> if they differ.</returns>
    public static bool operator !=(Id<TEntity> left, Id<TEntity> right) => !left.Equals(right);

    /// <summary>Orders two identities.</summary>
    /// <param name="left">The first identity.</param>
    /// <param name="right">The second identity.</param>
    /// <returns><see langword="true" /> if <paramref name="left" /> sorts first.</returns>
    public static bool operator <(Id<TEntity> left, Id<TEntity> right) => left.CompareTo(right) < 0;

    /// <summary>Orders two identities.</summary>
    /// <param name="left">The first identity.</param>
    /// <param name="right">The second identity.</param>
    /// <returns><see langword="true" /> if <paramref name="left" /> sorts first or they are equal.</returns>
    public static bool operator <=(Id<TEntity> left, Id<TEntity> right) => left.CompareTo(right) <= 0;

    /// <summary>Orders two identities.</summary>
    /// <param name="left">The first identity.</param>
    /// <param name="right">The second identity.</param>
    /// <returns><see langword="true" /> if <paramref name="left" /> sorts last.</returns>
    public static bool operator >(Id<TEntity> left, Id<TEntity> right) => left.CompareTo(right) > 0;

    /// <summary>Orders two identities.</summary>
    /// <param name="left">The first identity.</param>
    /// <param name="right">The second identity.</param>
    /// <returns><see langword="true" /> if <paramref name="left" /> sorts last or they are equal.</returns>
    public static bool operator >=(Id<TEntity> left, Id<TEntity> right) => left.CompareTo(right) >= 0;

    /// <inheritdoc />
    public override string ToString() => Value.ToString();
}
