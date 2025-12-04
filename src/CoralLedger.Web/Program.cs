using CoralLedger.Application;
using CoralLedger.Application.Common.Interfaces;
using CoralLedger.Infrastructure;
using CoralLedger.Infrastructure.Data;
using CoralLedger.Infrastructure.Data.Seeding;
using CoralLedger.Web.Components;
using CoralLedger.Web.Endpoints;
using CoralLedger.Web.Hubs;
using CoralLedger.Web.Security;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add Blazor components with Interactive Server mode + WebAssembly Auto mode
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents();

// Add Application layer (MediatR, FluentValidation)
builder.Services.AddApplication();

// Add Infrastructure layer services (including external API clients)
builder.Services.AddInfrastructure(builder.Configuration);

// Add infrastructure health checks
builder.Services.AddInfrastructureHealthChecks();

// Add background job scheduler (Quartz.NET)
builder.Services.AddQuartzJobs();

// Add SignalR for real-time notifications
builder.Services.AddSignalR();
builder.Services.AddScoped<IAlertHubContext, AlertHubContext>();

// Add Security: Rate limiting and CORS
builder.Services.AddSecurityRateLimiting();
builder.Services.AddSecurityCors(builder.Configuration);

// Add Performance: Response compression and caching
builder.Services.AddPerformanceCompression();

// Add OpenAPI documentation
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, context, cancellationToken) =>
    {
        document.Info = new()
        {
            Title = "CoralLedger Blue API",
            Version = "v1",
            Description = "Marine Protected Area monitoring and management API for The Bahamas. " +
                          "Provides endpoints for MPA data, vessel tracking, coral bleaching alerts, " +
                          "citizen observations, and AI-powered marine insights.",
            Contact = new()
            {
                Name = "CoralLedger Blue Team",
                Email = "api@coralledger.blue",
                Url = new Uri("https://coralledger.blue")
            },
            License = new()
            {
                Name = "MIT",
                Url = new Uri("https://opensource.org/licenses/MIT")
            }
        };
        return Task.CompletedTask;
    });
});

// Add Database with PostGIS support (skip in testing environment - tests configure their own)
if (!builder.Environment.IsEnvironment("Testing"))
{
    builder.AddMarineDatabase("marinedb");
}

var app = builder.Build();

// Initialize and seed database (skip in testing environment)
if (!app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<MarineDbContext>();

    // Ensure database is created and apply any pending migrations
    await context.Database.EnsureCreatedAsync();

    // Seed the database with Bahamas MPA data
    await BahamasMpaSeeder.SeedAsync(context);

    // Seed the database with Bahamian species
    await BahamianSpeciesSeeder.SeedAsync(context);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

// OpenAPI documentation (available in all environments)
app.MapOpenApi();
if (app.Environment.IsDevelopment())
{
    // Scalar API docs UI (only in development)
    app.MapScalarApiReference(options =>
    {
        options.WithTitle("CoralLedger Blue API");
        options.WithTheme(ScalarTheme.BluePlanet);
        options.WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);
    });
}

// Security middleware
app.UseSecurityHeaders();
app.UseCors();
app.UseRateLimiter();

// Performance middleware (order matters)
app.UseResponseCompression();
app.UseResponseCaching();

app.UseHttpsRedirection();
app.UseAntiforgery();

// Serve WebAssembly files
app.UseBlazorFrameworkFiles();
app.MapStaticAssets();

// Map API endpoints
app.MapMpaEndpoints();
app.MapVesselEndpoints();
app.MapBleachingEndpoints();
app.MapJobEndpoints();
app.MapObservationEndpoints();
app.MapAIEndpoints();
app.MapAlertEndpoints();
app.MapAisEndpoints();
app.MapExportEndpoints();
app.MapAdminEndpoints();
app.MapSpeciesEndpoints();

// Map SignalR hub
app.MapHub<AlertHub>("/hubs/alerts");

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(CoralLedger.Web.Client._Imports).Assembly);

app.MapDefaultEndpoints();

app.Run();

// Make Program accessible for integration testing
public partial class Program { }
