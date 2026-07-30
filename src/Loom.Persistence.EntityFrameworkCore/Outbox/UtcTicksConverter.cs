using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Loom.Persistence;

/// <summary>
/// Stores an instant as UTC ticks.
/// </summary>
/// <remarks>
/// Not a cosmetic choice. Providers differ on whether an offset-bearing instant can be ordered at all
/// — SQLite refuses to order by one — so a package that must order by time cannot store the native
/// type and remain provider-neutral. Ticks sort identically everywhere, and the conversion is exact.
/// </remarks>
internal sealed class UtcTicksConverter()
    : ValueConverter<DateTimeOffset, long>(
        value => value.UtcTicks,
        ticks => new DateTimeOffset(ticks, TimeSpan.Zero));
