<!--LOOM-TEMPLATE Archetype delta: background worker. Assembled by scripts/new-agents-md.sh. Does not govern the Loom repo itself. -->
## B. Worker archetype

Applies to `src/MyApp.Worker/`.

### Entry points

- **`BackgroundService` + `PeriodicTimer`.** No scheduling framework by default — most workers are "every N minutes" or "drain this," and the BCL does both with no dependencies.
- **Escalate to Quartz.NET only for a stated need:** cron expressions, clustering, or persistent job state. Adding it speculatively buys a database table and a configuration surface you do not want.
- **Hangfire is out.** `Hangfire.Core` is LGPL v3, which is a licence to adopt deliberately rather than inherit, and its Pro tier is paid. Its dashboard was compensating for missing observability, which Aspire already provides.
- **`ExecuteAsync` contains no business logic.** It ticks, runs one pass, and refuses to let a failure end the loop. Resolving a scope and dispatching to a handler belongs to a **pass** of its own — that is the position an endpoint occupies in the API archetype, and giving it a separate type is what lets either be tested without the other.

```csharp
// One pass: a scope, a dispatch, and what the outcome means. Everything here is decided by the
// result, so it can be exercised with no clock and no timer.
internal sealed class ReconcileOrdersPass(IServiceScopeFactory scopes)
{
    public async Task RunAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<IHandler<Request, Response>>();
        var result = await handler.HandleAsync(new Request(), ct);
        // map result category to retry / dead-letter / log — never throw to signal it
    }
}

// The schedule, and one guarantee: a failed pass must not end the loop.
internal sealed partial class ReconcileOrdersWorker(
    ReconcileOrdersPass pass,
    TimeProvider clock,
    ILogger<ReconcileOrdersWorker> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5), clock);
        while (await timer.WaitForNextTickAsync(ct))
        {
            await RunGuardedAsync(ct);
        }
    }

    // internal, not private: the testing rules below call this directly, so the test assembly needs
    // an InternalsVisibleTo.
    internal async Task RunGuardedAsync(CancellationToken ct)
    {
        try
        {
            await pass.RunAsync(ct);
        }
        catch (Exception exception)
            when (exception is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            // Swallowed, never silent. Only cancellation that shutdown asked for ends the loop.
            Failed(logger, exception);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "A pass threw and was swallowed to keep the worker alive.")]
    private static partial void Failed(ILogger logger, Exception exception);
}
```

- **Resolve a new DI scope per iteration.** A `DbContext` captured across iterations accumulates tracked entities and will eventually behave incorrectly. This is the most common worker defect.
- **Pass the `CancellationToken` everywhere and honour it.** A worker that ignores shutdown gets killed mid-transaction.
- **Inject `TimeProvider`, never `DateTime.Now`.** It is also what makes the timer testable.

### Error handling

- **A failed iteration must not kill the worker.** An unhandled exception out of `ExecuteAsync` stops the service silently in some hosting models. Catch, log, and continue.
- **The `Loom.Results` category decides the response:** `Unavailable` retries with backoff; `Invalid` and `Conflict` dead-letter, because retrying will not change the outcome; `NotFound` is usually a no-op worth logging once.
- **Retries are bounded and logged.** Unbounded retry against a permanent failure is an outage with extra steps.

### Testing

- **Handlers are tested directly**, against Testcontainers Postgres, with Respawn between tests. There is no transport to go through, so the handler *is* the entry point.
- **Separate the pass from the schedule, and test the pass.** The pass holds everything decided by a result — the scope, the dispatch, retry against dead-letter — so it can be exercised against a stub handler with no clock and no timer. Make the retry backoff a setting so a test can set it to zero.
- **Test one guarded iteration, not the timer.** Extract the `try`/`catch` into an `internal` method — with `<InternalsVisibleTo Include="MyApp.Worker.Tests" />` on the host project — so a test can call it directly and assert which exceptions survive it. That a failing pass does not end the loop is your logic; that `PeriodicTimer` fires is the BCL's. Driving a real `BackgroundService` from a fake clock needs the scheduler to hand off between advancing the clock and the loop resuming — a test that does it is flaky unless it sleeps, and then it is a slow test of someone else's code.
