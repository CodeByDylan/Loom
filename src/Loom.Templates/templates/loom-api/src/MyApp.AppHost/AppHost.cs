using Aspire.Hosting;

// Development-time orchestration only. Nothing here ships: it starts a database and the service, and
// wires the connection string between them so that no developer has to keep one in a file.
IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

// Resource names are deliberately not derived from the project name. Aspire allows only letters,
// digits and hyphens, and a solution called Acme.Billing would produce "acme.billing" — rejected at
// build time. These are local to the AppHost and referenced by name, so nothing is gained by
// repeating the application's name in them.
var postgres = builder.AddPostgres("postgres").WithDataVolume();

var database = postgres.AddDatabase("database");

// HostProject is substituted with the solution name, dots replaced by underscores, because that is
// how the Aspire SDK names the class it generates for a project reference.
builder.AddProject<Projects.HostProject_Api>("api")
    .WithReference(database)
    .WaitFor(database);

await builder.Build().RunAsync();
