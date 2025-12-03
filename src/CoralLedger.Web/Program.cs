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

// Add Infrastructure layer services
builder.Services.AddInfrastructure();

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

// Map API endpoints for MPA data
app.MapMpaEndpoints();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(CoralLedger.Web.Client._Imports).Assembly);

app.MapDefaultEndpoints();

app.Run();
