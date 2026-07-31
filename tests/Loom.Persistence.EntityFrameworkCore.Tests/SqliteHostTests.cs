using Microsoft.Extensions.DependencyInjection;

namespace Loom.Persistence.EntityFrameworkCore.Tests;

public sealed class SqliteHostTests
{
    [Test]
    public async Task A_Host_That_Fails_To_Initialise_Cleans_Up_And_Reports_Why()
    {
        // The factory never returns the host when initialisation throws, so the cleanup has no second
        // chance — if it threw itself, or swallowed, this is where it would show.
        InvalidOperationException thrown = new("registration refused");

        await Assert.That(async () => await TestHost.CreateAsync(_ => throw thrown))
            .ThrowsExactly<InvalidOperationException>();
    }
}
