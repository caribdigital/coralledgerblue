var builder = DistributedApplication.CreateBuilder(args);

// PostgreSQL with PostGIS - using official PostGIS Docker image
var postgres = builder.AddPostgres("postgres")
    .WithImage("postgis/postgis")
    .WithImageTag("16-3.4")
    .WithDataVolume("coralledger-postgres-data")
    .WithPgAdmin();

// Create the marine database
var marineDb = postgres.AddDatabase("marinedb");

// Web application with database connection
builder.AddProject<Projects.CoralLedger_Web>("web")
    .WithReference(marineDb)
    .WaitFor(marineDb)
    .WithExternalHttpEndpoints();

builder.Build().Run();
