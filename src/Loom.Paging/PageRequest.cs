namespace Loom.Paging;

/// <summary>
/// A request for one page of results, validated on construction.
/// </summary>
/// <remarks>
/// Offset-based. Jumping straight to page twenty is possible, at the cost of two known weaknesses:
/// deep offsets get slower, and rows shift between pages when the underlying data changes. Cursor
/// paging avoids both but needs a stable ordering key, which this design has no obvious candidate
/// for — identities are only millisecond-granular and are explicitly not creation-ordered.
/// <para>
/// The size limit is enforced here rather than left to a validator, because an unbounded page size
/// is a denial-of-service vector: one request asking for a million rows will happily return the
/// table.
/// </para>
/// </remarks>
public sealed record PageRequest
{
    /// <summary>
    /// The largest page size allowed unless a different limit is given.
    /// </summary>
    public const int DefaultMaximumSize = 100;

    /// <summary>
    /// Creates a page request.
    /// </summary>
    /// <param name="number">The one-based page number.</param>
    /// <param name="size">How many items the page holds.</param>
    /// <param name="maximumSize">The largest size this request may ask for.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="number" /> is less than one, <paramref name="size" /> is less than one or
    /// greater than <paramref name="maximumSize" />, or <paramref name="maximumSize" /> is less than
    /// one.
    /// </exception>
    public PageRequest(int number, int size, int maximumSize = DefaultMaximumSize)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumSize, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(number, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(size, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(size, maximumSize);

        Number = number;
        Size = size;
        MaximumSize = maximumSize;
    }

    /// <summary>Gets the one-based page number.</summary>
    public int Number { get; }

    /// <summary>Gets how many items the page holds.</summary>
    public int Size { get; }

    /// <summary>Gets the largest size this request was permitted to ask for.</summary>
    public int MaximumSize { get; }

    /// <summary>Gets how many items precede this page.</summary>
    public int Skip => (Number - 1) * Size;

    /// <summary>Gets the first page, at the default size.</summary>
    public static PageRequest First => new(number: 1, size: DefaultMaximumSize);
}
