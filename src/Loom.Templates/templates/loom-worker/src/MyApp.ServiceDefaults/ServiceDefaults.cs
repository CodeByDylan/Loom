using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Telemetry, health, resilience and service discovery, configured once for every service.
/// </summary>
/// <remarks>
/// This file is part of the application rather than a package: it is scaffolded in so that it can be
/// read and changed. That is why the orchestration choice survives the objection that ruled out other
/// frameworks — nothing here sits in the request path as a dependency you cannot see.
/// </remarks>
public static class ServiceDefaults
{
    /// <summary>
    /// Adds telemetry, a liveness check, service discovery and resilient HTTP defaults.
    /// </summary>
    /// <typeparam name="TBuilder">The builder being configured.</typeparam>
    /// <param name="builder">The application builder.</param>
    /// <returns>The same builder, so calls can be chained.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder" /> is <see langword="null" />.</exception>
    /// <remarks>
    /// The one call every service makes. Outgoing HTTP clients get retries and a circuit breaker by
    /// default, so a service that forgets to ask for resilience still has it.
    /// </remarks>
    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ConfigureOpenTelemetry();

        builder.Services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);

        builder.Services.AddServiceDiscovery();

        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            http.AddStandardResilienceHandler();
            http.AddServiceDiscovery();
        });

        return builder;
    }

    /// <summary>
    /// Turns on structured logs, metrics and traces, exporting them only if a collector is configured.
    /// </summary>
    /// <typeparam name="TBuilder">The builder being configured.</typeparam>
    /// <param name="builder">The application builder.</param>
    /// <returns>The same builder, so calls can be chained.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder" /> is <see langword="null" />.</exception>
    /// <remarks>
    /// Instrumentation is always collected; the OTLP exporter is added only when
    /// <c>OTEL_EXPORTER_OTLP_ENDPOINT</c> is set. That way a service run on its own does not spend the
    /// application's startup failing to reach a collector that is not there.
    /// </remarks>
    public static TBuilder ConfigureOpenTelemetry<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        builder.Services.AddOpenTelemetry()
            .WithMetrics(metrics => metrics
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation())
            .WithTracing(tracing => tracing
                .AddHttpClientInstrumentation());

        if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
        {
            builder.Services.AddOpenTelemetry().UseOtlpExporter();
        }

        return builder;
    }
}
