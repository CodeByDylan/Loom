<!--LOOM-TEMPLATE Archetype delta: CLI. Assembled by scripts/new-agents-md.sh. Does not govern the Loom repo itself. -->
## C. CLI archetype

Applies to `src/MyApp.Cli/`.

### Entry points

- **`System.CommandLine` for parsing.** First-party, GA since 2.0, trimming-friendly, and it gives help and shell completion for free.
- **`Spectre.Console` is permitted for *rendering* only** — tables, progress, prompts. Not `Spectre.Console.Cli`: taking a third-party command framework is the coupling this stack avoids everywhere else.
- **A command handler contains no business logic.** It parses, dispatches to an `IHandler<,>`, maps the result to output and an exit code. Same thin-adapter rule as an endpoint.
- **The same handlers the API and worker use.** If a CLI command needs logic that does not exist as a handler, write the handler — do not inline it into the command.

The shape a command takes — illustrative, not compilable: `ToExitCode()` does not ship yet (see the
callout under *Output and exit codes*).

```csharp
internal static class ReconcileCommand
{
    public static Command Build(IServiceProvider services)
    {
        var command = new Command("reconcile", "Reconcile outstanding orders.");
        command.SetAction(async (parse, ct) =>
        {
            var handler = services.GetRequiredService<IHandler<Request, Response>>();
            var result = await handler.HandleAsync(new Request(), ct);
            return result.ToExitCode();
        });
        return command;
    }
}
```

### Output and exit codes

- **Exit codes come from the `Loom.Results` category**, mapped in one place, the same way `ToHttpResult()` works in the API: `0` success, `1` unexpected failure, and a stable code per category. Never `Environment.Exit` from inside a handler.

> **Not shipped yet:** no package provides `ToExitCode()` today — it arrives with the `loom-cli`
> archetype (see the roadmap). Until it does, this archetype cannot satisfy the rule that
> category-to-transport mapping comes from a Loom package, which is one reason the archetype does
> not exist yet either. Do not write the mapping inline to bridge the gap.
- **Human output on stdout, diagnostics on stderr.** A CLI whose logs pollute stdout cannot be piped.
- **Machine-readable output is opt-in** via `--json`, and when requested it is the *only* thing on stdout. No banners, no progress bars, no colour.
- **Respect `NO_COLOR` and non-interactive terminals.** Detect redirected output and drop styling.

### Hosting

- **No Aspire.** `AppHost` and `ServiceDefaults` are for long-running services; a CLI gets a plain `HostApplicationBuilder`, console logging, and no OpenTelemetry.
- **Configuration precedence is command-line flags, then environment variables, then config files.** A flag must always win.
- **Secrets never come from flags.** They are visible in shell history and process listings — use environment variables.

### Testing

- **Handlers are tested directly**, as in the worker archetype.
- **Test argument parsing separately** by invoking the built `Command` with an argument array and asserting the parse result. Do not shell out to the built binary.
