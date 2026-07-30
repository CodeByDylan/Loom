using Loom.Paging;
using Microsoft.EntityFrameworkCore;

namespace Loom.Persistence;

/// <summary>
/// Reads pages from Entity Framework Core queries.
/// </summary>
public static class PagingQueryableExtensions
{
    /// <summary>
    /// Counts the query and reads one page of it.
    /// </summary>
    /// <typeparam name="T">The type being queried.</typeparam>
    /// <param name="source">The query to page.</param>
    /// <param name="request">The page to read.</param>
    /// <param name="cancellationToken">Cancels both queries.</param>
    /// <returns>The requested page, with the total count.</returns>
    /// <exception cref="ArgumentNullException">Either argument is <see langword="null" />.</exception>
    /// <remarks>
    /// Two round trips: one to count, one to fetch. The count is skipped when the requested page
    /// starts beyond the end of a short result set, since the count is already known by then.
    /// </remarks>
    public static async Task<Page<T>> ToPageAsync<T>(
        this IQueryable<T> source,
        PageRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(request);

        int total = await source.CountAsync(cancellationToken);

        if (request.Skip >= total)
        {
            return new Page<T>([], total, request.Number, request.Size);
        }

        List<T> items = await source.ApplyPaging(request).ToListAsync(cancellationToken);

        return new Page<T>(items, total, request.Number, request.Size);
    }
}
