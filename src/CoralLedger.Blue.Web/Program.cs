using System.Globalization;
using CoralLedger.Blue.Application;
using CoralLedger.Blue.Application.Common.Interfaces;
using CoralLedger.Blue.Infrastructure;
using CoralLedger.Blue.Infrastructure.Data;
using CoralLedger.Blue.Infrastructure.Data.Seeding;
using CoralLedger.Blue.Web.Components;
using CoralLedger.Blue.Web.Endpoints;
using CoralLedger.Blue.Web.Hubs;
using CoralLedger.Blue.Web.Security;
using CoralLedger.Blue.Web.Theme;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using Radzen;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Provide the shared UI theme for CSS variable overrides.
builder.Services.AddSingleton(CoralLedgerTheme.Dark);
builder.Services.AddScoped<IThemeState, ThemeState>();

// Add Radzen Blazor services
builder.Services.AddRadzenComponents();

// Add Localization support (English, Spanish, Haitian Creole)
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var supportedCultures = new[]
    {
        new CultureInfo("en"),    // English (default)
        new CultureInfo("es"),    // Spanish
        new CultureInfo("ht")     // Haitian Creole
    };

    options.DefaultRequestCulture = new RequestCulture("en");
    options.SupportedCultures = supportedCultures;
    options.SupportedUICultures = supportedCultures;

    // Accept language from: query string (?culture=es), cookie, or Accept-Language header
    options.RequestCultureProviders = new List<IRequestCultureProvider>
    {
        new QueryStringRequestCultureProvider(),
        new CookieRequestCultureProvider(),
        new AcceptLanguageHeaderRequestCultureProvider()
    };
});

// Add Blazor components with Interactive Server mode + WebAssembly Auto mode
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents();

// Add Application layer (MediatR, FluentValidation)
builder.Services.AddApplication();

// Add HttpClient factory for Blazor components
builder.Services.AddHttpClient();

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
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    // Apply pending migrations (creates database if it doesn't exist)
    // Note: MigrateAsync() is idempotent - only applies migrations not yet applied
    var pendingMigrations = await context.Database.GetPendingMigrationsAsync();
    if (pendingMigrations.Any())
    {
        logger.LogInformation("Applying {Count} pending migration(s)...", pendingMigrations.Count());
        await context.Database.MigrateAsync();
        logger.LogInformation("Database migrations applied successfully");
    }
    else
    {
        logger.LogInformation("Database is up to date, no migrations to apply");
    }

    // Seed the database with Bahamas MPA data (idempotent - checks if data exists)
    await BahamasMpaSeeder.SeedAsync(context);

    // Seed the database with Bahamian species (idempotent - checks if data exists)
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

// ============================================================
// MIDDLEWARE ORDER IS CRITICAL - Follow this sequence exactly
// ============================================================

// 1. Core performance and security headers that don't depend on routing/endpoints
app.UseResponseCompression();
app.UseResponseCaching();
app.UseHttpsRedirection();
app.UseSecurityHeaders();

// 2. Static files - UseStaticFiles for wwwroot, UseBlazorFrameworkFiles for WASM
app.UseStaticFiles();
app.UseBlazorFrameworkFiles();

// 3. Localization (must be before routing for culture-aware routes)
app.UseRequestLocalization();

// 4. Routing begins here - explicitly add UseRouting()
app.UseRouting();

// 4. Middleware that needs to know about the selected endpoint
//    These MUST be after UseRouting() and before endpoint execution
app.UseCors();
app.UseRateLimiter();
app.UseAntiforgery();

// 5. Map Razor Components (handles Blazor routes)
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(CoralLedger.Blue.Web.Client._Imports).Assembly);

// 7. Map API endpoints
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
app.MapDiagnosticsEndpoints();

// 8. Map SignalR hub
app.MapHub<AlertHub>("/hubs/alerts");

// 9. Map default endpoints (health checks, etc.) - should be last
app.MapDefaultEndpoints();

app.Run();

// Make Program accessible for integration testing
public partial class Program { }
