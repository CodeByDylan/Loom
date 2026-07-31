using Loom.Handlers;
using Loom.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MyApp.Domain.Widgets;
using MyApp.Worker.Features.Widgets.RetireOversizedWidgets;

namespace MyApp.Worker.Tests;

/// <summary>
/// The slice, exercised through the same decorator chain the worker resolves at run time.
/// </summary>
[NotInParallel]
public sealed class RetireOversizedWidgetsTests
{
    private WorkerHost _host = null!;

    [Before(Test)]
    public async Task ResetAsync()
    {
        _host = WorkerFixture.Host;
        await _host.ResetAsync();
    }

    [Test]
    public async Task Oversized_Widgets_Are_Retired_And_Others_Are_Left_Alone()
    {
        await _host.InDatabaseAsync(async database =>
        {
            database.Widgets.Add(Widget.Create("big", 500).Value);
            database.Widgets.Add(Widget.Create("small", 5).Value);
            await database.SaveChangesAsync();
        });

        Result<Response> result = await DispatchAsync(new Request(LargerThan: 100, BatchSize: 500));

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value.Retired).IsEqualTo(1);

        await _host.InDatabaseAsync(async database =>
        {
            await Assert.That(await database.Widgets.CountAsync(widget => widget.IsRetired)).IsEqualTo(1);
            await Assert.That(await database.Widgets.CountAsync(widget => !widget.IsRetired)).IsEqualTo(1);
        });
    }

    [Test]
    public async Task Running_Twice_Retires_Nothing_The_Second_Time()
    {
        await _host.InDatabaseAsync(async database =>
        {
            database.Widgets.Add(Widget.Create("big", 500).Value);
            await database.SaveChangesAsync();
        });

        Result<Response> first = await DispatchAsync(new Request(LargerThan: 100, BatchSize: 500));
        Result<Response> second = await DispatchAsync(new Request(LargerThan: 100, BatchSize: 500));

        await Assert.That(first.Value.Retired).IsEqualTo(1);

        // A scheduled operation runs again and again, so doing nothing the second time is the
        // behaviour that matters most, not a detail.
        await Assert.That(second.Value.Retired).IsEqualTo(0);
    }

    [Test]
    public async Task An_Invalid_Request_Is_Refused_By_The_Decorator()
    {
        // Rejected by the validator before the handler runs. The handler would happily accept it,
        // which is what makes this a test of the chain rather than of the query.
        Result<Response> result = await DispatchAsync(new Request(LargerThan: 0, BatchSize: 500));

        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(result.Error.Category).IsEqualTo(ErrorCategory.Invalid);
    }

    [Test]
    public async Task A_Pass_Retires_At_Most_One_Batch()
    {
        await _host.InDatabaseAsync(async database =>
        {
            for (int i = 0; i < 5; i++)
            {
                database.Widgets.Add(Widget.Create($"big-{i}", 500 + i).Value);
            }

            await database.SaveChangesAsync();
        });

        Result<Response> result = await DispatchAsync(new Request(LargerThan: 100, BatchSize: 2));

        // Bounded, so a backlog is worked through over several ticks rather than loaded at once.
        await Assert.That(result.Value.Retired).IsEqualTo(2);

        await _host.InDatabaseAsync(async database =>
            await Assert.That(await database.Widgets.CountAsync(widget => !widget.IsRetired)).IsEqualTo(3));
    }

    private async Task<Result<Response>> DispatchAsync(Request request)
    {
        using IServiceScope scope = _host.CreateScope();

        return await scope.ServiceProvider
            .GetRequiredService<IHandler<Request, Response>>()
            .HandleAsync(request, CancellationToken.None);
    }
}
