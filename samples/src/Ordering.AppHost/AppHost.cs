using Aspire.Hosting;

// Development-time orchestration only. Nothing here ships: it starts a database and the service, and
// wires the connection string between them so that no developer has to keep one in a file.
IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .WithDataVolume()
    .WithPgAdmin();

var ordering = postgres.AddDatabase("ordering");

builder.AddProject<Projects.Ordering_Api>("ordering-api")
    .WithReference(ordering)
    .WaitFor(ordering);

await builder.Build().RunAsync();
