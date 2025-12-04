using CoralLedger.Application;
using CoralLedger.Infrastructure;
using CoralLedger.Infrastructure.Data;
using CoralLedger.Infrastructure.Data.Seeding;
using CoralLedger.Web.Components;
using CoralLedger.Web.Endpoints;

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

// Add background job scheduler (Quartz.NET)
builder.Services.AddQuartzJobs();

// Add Database with PostGIS support
builder.AddMarineDatabase("marinedb");

var app = builder.Build();

// Initialize and seed database
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<MarineDbContext>();

    // Ensure database is created and apply any pending migrations
    await context.Database.EnsureCreatedAsync();

    // Seed the database with Bahamas MPA data
    await BahamasMpaSeeder.SeedAsync(context);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

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

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(CoralLedger.Web.Client._Imports).Assembly);

app.MapDefaultEndpoints();

app.Run();
