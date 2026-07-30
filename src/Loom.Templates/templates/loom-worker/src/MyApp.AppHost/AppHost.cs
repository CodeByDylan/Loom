using Aspire.Hosting;

// Development-time orchestration only. Nothing here ships: it starts a database and the worker, and
// wires the connection string between them so that no developer has to keep one in a file.
IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

// Resource names are deliberately not derived from the project name. Aspire allows only letters,
// digits and hyphens, and a solution called Acme.Billing would produce "acme.billing" — rejected at
// build time.
var postgres = builder.AddPostgres("postgres").WithDataVolume();

var database = postgres.AddDatabase("database");

// HostProject is substituted with the solution name, dots replaced by underscores, because that is
// how the Aspire SDK names the class it generates for a project reference.
builder.AddProject<Projects.HostProject_Worker>("worker")
    .WithReference(database)
    .WaitFor(database);

await builder.Build().RunAsync();
