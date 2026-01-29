var builder = DistributedApplication.CreateBuilder(args);

// PostgreSQL with PostGIS - using official PostGIS Docker image
// Use fixed container name + persistent lifetime to reuse existing container
var postgres = builder.AddPostgres("postgres")
    .WithImage("postgis/postgis")
    .WithImageTag("16-3.4")
    .WithContainerName("coralledger-blue-postgres")  // Fixed name for reuse
    .WithDataVolume("coralledger-postgres-data")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithPgAdmin(c => c
        .WithContainerName("coralledger-blue-pgadmin")  // Fixed name for reuse
        .WithLifetime(ContainerLifetime.Persistent));

// Create the marine database
var marineDb = postgres.AddDatabase("marinedb");

// Web application with database connection
builder.AddProject<Projects.CoralLedger_Blue_Web>("web")
    .WithReference(marineDb)
    .WaitFor(marineDb)
    .WithExternalHttpEndpoints();

builder.Build().Run();
