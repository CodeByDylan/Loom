namespace Loom.Paging;

/// <summary>
/// Applies page requests to sequences.
/// </summary>
public static class PagingExtensions
{
    /// <summary>
    /// Narrows a query to one page. Does not count, so the caller can count once and reuse it.
    /// </summary>
    /// <typeparam name="T">The type being queried.</typeparam>
    /// <param name="source">The query to narrow.</param>
    /// <param name="request">The page to take.</param>
    /// <returns>A query limited to the requested page.</returns>
    /// <exception cref="ArgumentNullException">Either argument is <see langword="null" />.</exception>
    public static IQueryable<T> ApplyPaging<T>(this IQueryable<T> source, PageRequest request)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(request);

        return source.Skip(request.Skip).Take(request.Size);
    }

    /// <summary>
    /// Narrows an in-memory sequence to one page.
    /// </summary>
    /// <typeparam name="T">The type being paged.</typeparam>
    /// <param name="source">The sequence to narrow.</param>
    /// <param name="request">The page to take.</param>
    /// <returns>A sequence limited to the requested page.</returns>
    /// <exception cref="ArgumentNullException">Either argument is <see langword="null" />.</exception>
    public static IEnumerable<T> ApplyPaging<T>(this IEnumerable<T> source, PageRequest request)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(request);

        return source.Skip(request.Skip).Take(request.Size);
    }

    /// <summary>
    /// Counts an in-memory sequence and takes one page of it.
    /// </summary>
    /// <typeparam name="T">The type being paged.</typeparam>
    /// <param name="source">The sequence to page.</param>
    /// <param name="request">The page to take.</param>
    /// <returns>The requested page, with the total count.</returns>
    /// <exception cref="ArgumentNullException">Either argument is <see langword="null" />.</exception>
    /// <remarks>
    /// Enumerates <paramref name="source" /> more than once. For a database query, use the
    /// asynchronous equivalent from the Loom persistence package, which counts and fetches without
    /// materialising everything.
    /// </remarks>
    public static Page<T> ToPage<T>(this IEnumerable<T> source, PageRequest request)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(request);

        T[] materialised = [.. source];

        return new Page<T>(
            [.. materialised.ApplyPaging(request)],
            materialised.Length,
            request.Number,
            request.Size);
    }
}
