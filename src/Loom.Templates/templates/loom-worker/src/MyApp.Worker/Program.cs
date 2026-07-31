using MyApp.Worker;
using MyApp.Worker.Workers;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

// Telemetry, health, resilience and service discovery, from the scaffolded defaults project.
builder.AddServiceDefaults();

// Read once and refused here, so the failure lands at startup rather than on the first iteration.
string connectionString = builder.Configuration.GetConnectionString("database")
    ?? throw new InvalidOperationException(
        "No 'database' connection string was configured. The AppHost provides one when running "
        + "locally; a deployment supplies it through configuration.");

builder.Services.AddWorkerServices(connectionString, builder.Configuration);

builder.Services.AddHostedService<RetireWidgetsWorker>();

IHost host = builder.Build();

await host.RunAsync();

// Exposed so the test assembly can name this one.
public partial class Program;
