namespace Loom.Paging;

/// <summary>
/// One page of results, together with how many there are in total.
/// </summary>
/// <typeparam name="T">The type of item on the page.</typeparam>
/// <remarks>
/// This is the shape that appears in a slice's response, so that every project reports paging the
/// same way instead of inventing its own envelope.
/// <para>
/// The total count means a second query. Where that is too expensive, a slice should write its own
/// query and return its own shape — the database context is always directly available to it.
/// </para>
/// </remarks>
/// <param name="Items">The items on this page.</param>
/// <param name="TotalCount">How many items exist across all pages.</param>
/// <param name="Number">The one-based number of this page.</param>
/// <param name="Size">The page size that was requested.</param>
public sealed record Page<T>(IReadOnlyList<T> Items, int TotalCount, int Number, int Size)
{
    /// <summary>Gets how many pages exist in total.</summary>
    public int TotalPages => TotalCount is 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)Size);

    /// <summary>Gets a value indicating whether a page precedes this one.</summary>
    public bool HasPrevious => Number > 1;

    /// <summary>Gets a value indicating whether a page follows this one.</summary>
    public bool HasNext => Number < TotalPages;

    /// <summary>
    /// Creates an empty page for a request that matched nothing.
    /// </summary>
    /// <param name="request">The request that matched nothing.</param>
    /// <returns>An empty page.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request" /> is <see langword="null" />.</exception>
    public static Page<T> Empty(PageRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return new Page<T>([], TotalCount: 0, request.Number, request.Size);
    }
}
