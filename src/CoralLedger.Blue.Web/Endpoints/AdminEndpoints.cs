using CoralLedger.Blue.Application.Common.Interfaces;
using CoralLedger.Blue.Domain.Entities;
using CoralLedger.Blue.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;

namespace CoralLedger.Blue.Web.Endpoints;

public static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/admin")
            .WithTags("Administration");

        // GET /api/admin/dashboard - Admin dashboard stats
        group.MapGet("/dashboard", async (
            IMarineDbContext context,
            CancellationToken ct = default) =>
        {
            var mpaCount = await context.MarineProtectedAreas.CountAsync(ct);
            var reefCount = await context.Reefs.CountAsync(ct);
            var vesselCount = await context.Vessels.CountAsync(ct);
            var observationCount = await context.CitizenObservations.CountAsync(ct);
            var alertRuleCount = await context.AlertRules.CountAsync(ct);
            var activeAlertCount = await context.Alerts.CountAsync(a => !a.IsAcknowledged, ct);

            var recentBleaching = await context.BleachingAlerts
                .Where(b => b.Date >= DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-7)))
                .CountAsync(ct);

            var recentEvents = await context.VesselEvents
                .Where(e => e.StartTime >= DateTime.UtcNow.AddDays(-7))
                .CountAsync(ct);

            var pendingObservations = await context.CitizenObservations
                .CountAsync(o => o.Status == ObservationStatus.Pending, ct);

            return Results.Ok(new
            {
                counts = new
                {
                    mpas = mpaCount,
                    reefs = reefCount,
                    vessels = vesselCount,
                    observations = observationCount,
                    alertRules = alertRuleCount,
                    activeAlerts = activeAlertCount
                },
                recent = new
                {
                    bleachingAlerts = recentBleaching,
                    vesselEvents = recentEvents,
                    pendingObservations = pendingObservations
                },
                lastUpdated = DateTime.UtcNow
            });
        })
        .WithName("GetAdminDashboard")
        .Produces<object>();

        // GET /api/admin/observations/pending - Get pending observations for moderation
        group.MapGet("/observations/pending", async (
            IMarineDbContext context,
            int page = 1,
            int pageSize = 20,
            CancellationToken ct = default) =>
        {
            var query = context.CitizenObservations
                .Include(o => o.Photos)
                .Where(o => o.Status == ObservationStatus.Pending)
                .OrderByDescending(o => o.CreatedAt);

            var total = await query.CountAsync(ct);
            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(o => new
                {
                    o.Id,
                    o.Title,
                    o.Description,
                    Type = o.Type.ToString(),
                    o.Severity,
                    o.ObservationTime,
                    o.CreatedAt,
                    o.CitizenEmail,
                    o.CitizenName,
                    PhotoCount = o.Photos.Count,
                    Location = o.Location != null ? new { Lon = o.Location.X, Lat = o.Location.Y } : null
                })
                .ToListAsync(ct);

            return Results.Ok(new
            {
                items,
                total,
                page,
                pageSize,
                totalPages = (int)Math.Ceiling(total / (double)pageSize)
            });
        })
        .WithName("GetPendingObservations")
        .Produces<object>();

        // POST /api/admin/observations/{id}/approve - Approve an observation
        group.MapPost("/observations/{id:guid}/approve", async (
            Guid id,
            ApproveRequest? request,
            IMarineDbContext context,
            CancellationToken ct = default) =>
        {
            var observation = await context.CitizenObservations.FindAsync(new object[] { id }, ct);
            if (observation == null)
                return Results.NotFound();

            observation.Approve(request?.Notes);
            await context.SaveChangesAsync(ct);

            return Results.Ok(new { observation.Id, Status = "Approved" });
        })
        .WithName("ApproveObservation")
        .Produces<object>()
        .Produces(StatusCodes.Status404NotFound);

        // POST /api/admin/observations/{id}/reject - Reject an observation
        group.MapPost("/observations/{id:guid}/reject", async (
            Guid id,
            RejectRequest request,
            IMarineDbContext context,
            CancellationToken ct = default) =>
        {
            var observation = await context.CitizenObservations.FindAsync(new object[] { id }, ct);
            if (observation == null)
                return Results.NotFound();

            if (string.IsNullOrWhiteSpace(request.Reason))
                return Results.BadRequest("Rejection reason is required");

            observation.Reject(request.Reason);
            await context.SaveChangesAsync(ct);

            return Results.Ok(new { observation.Id, Status = "Rejected" });
        })
        .WithName("RejectObservation")
        .Produces<object>()
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound);

        // GET /api/admin/system/health - System health check
        group.MapGet("/system/health", async (
            IMarineDbContext context,
            CancellationToken ct = default) =>
        {
            var dbHealthy = false;
            try
            {
                await context.Database.ExecuteSqlRawAsync("SELECT 1", ct);
                dbHealthy = true;
            }
            catch { }

            return Results.Ok(new
            {
                status = dbHealthy ? "Healthy" : "Unhealthy",
                database = dbHealthy ? "Connected" : "Disconnected",
                timestamp = DateTime.UtcNow
            });
        })
        .WithName("GetSystemHealth")
        .Produces<object>();

        // GET /api/admin/system/config - Get system configuration
        group.MapGet("/system/config", (IConfiguration configuration) =>
        {
            return Results.Ok(new
            {
                features = new
                {
                    aiEnabled = !string.IsNullOrEmpty(configuration["MarineAI:ApiKey"]),
                    aisEnabled = configuration.GetValue<bool>("AIS:Enabled"),
                    blobStorageConfigured = !string.IsNullOrEmpty(configuration["BlobStorage:ConnectionString"]),
                    gfwConfigured = !string.IsNullOrEmpty(configuration["GlobalFishingWatch:ApiKey"])
                },
                environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"
            });
        })
        .WithName("GetSystemConfig")
        .Produces<object>();

        // GET /api/admin/jobs - Get background job status
        group.MapGet("/jobs", async (
            IMarineDbContext context,
            CancellationToken ct = default) =>
        {
            // Get latest sync timestamps from data
            var latestBleaching = await context.BleachingAlerts
                .OrderByDescending(b => b.Date)
                .Select(b => b.Date)
                .FirstOrDefaultAsync(ct);

            var latestVesselEvent = await context.VesselEvents
                .OrderByDescending(e => e.CreatedAt)
                .Select(e => e.CreatedAt)
                .FirstOrDefaultAsync(ct);

            return Results.Ok(new
            {
                jobs = new[]
                {
                    new { name = "BleachingDataSync", schedule = "Daily 6:00 UTC", lastData = latestBleaching.ToString("yyyy-MM-dd") },
                    new { name = "VesselEventSync", schedule = "Every 6 hours", lastData = latestVesselEvent.ToString("o") }
                }
            });
        })
        .WithName("GetAdminJobs")
        .Produces<object>();

        // GET /api/admin/data/summary - Data summary statistics
        group.MapGet("/data/summary", async (
            IMarineDbContext context,
            CancellationToken ct = default) =>
        {
            var mpasByIsland = await context.MarineProtectedAreas
                .GroupBy(m => m.IslandGroup)
                .Select(g => new { IslandGroup = g.Key.ToString(), Count = g.Count(), TotalAreaKm2 = g.Sum(m => m.AreaSquareKm) })
                .ToListAsync(ct);

            var mpasByProtection = await context.MarineProtectedAreas
                .GroupBy(m => m.ProtectionLevel)
                .Select(g => new { Level = g.Key.ToString(), Count = g.Count() })
                .ToListAsync(ct);

            var observationsByType = await context.CitizenObservations
                .GroupBy(o => o.Type)
                .Select(g => new { Type = g.Key.ToString(), Count = g.Count() })
                .ToListAsync(ct);

            var observationsByStatus = await context.CitizenObservations
                .GroupBy(o => o.Status)
                .Select(g => new { Status = g.Key.ToString(), Count = g.Count() })
                .ToListAsync(ct);

            return Results.Ok(new
            {
                mpas = new { byIslandGroup = mpasByIsland, byProtectionLevel = mpasByProtection },
                observations = new { byType = observationsByType, byStatus = observationsByStatus }
            });
        })
        .WithName("GetDataSummary")
        .Produces<object>();

        // POST /api/admin/dev/seed-fishing-events - Seed sample fishing events for development
        group.MapPost("/dev/seed-fishing-events", async (
            IMarineDbContext context,
            CancellationToken ct = default) =>
        {
            var geometryFactory = new GeometryFactory(new PrecisionModel(), 4326);
            var random = new Random(42); // Fixed seed for reproducible data

            // Check if sample vessels already exist
            var existingVessel = await context.Vessels
                .FirstOrDefaultAsync(v => v.Name == "F/V Sample Trawler", ct);

            if (existingVessel != null)
            {
                return Results.Ok(new
                {
                    message = "Sample fishing data already exists",
                    vesselsCreated = 0,
                    eventsCreated = 0
                });
            }

            // Create sample vessels with Bahamian and Caribbean flags
            var sampleVessels = new[]
            {
                Vessel.Create("F/V Sample Trawler", mmsi: "311000001", flag: "BHS", vesselType: VesselType.Fishing, gearType: GearType.Trawlers),
                Vessel.Create("F/V Nassau Pride", mmsi: "311000002", flag: "BHS", vesselType: VesselType.Fishing, gearType: GearType.Longliners),
                Vessel.Create("F/V Caribbean Star", mmsi: "352000001", flag: "PAN", vesselType: VesselType.Fishing, gearType: GearType.PurseSeiners),
                Vessel.Create("F/V Freeport Fisher", mmsi: "311000003", flag: "BHS", vesselType: VesselType.Fishing, gearType: GearType.Trawlers),
                Vessel.Create("F/V Island Catch", mmsi: "319000001", flag: "CYM", vesselType: VesselType.Fishing, gearType: GearType.Longliners)
            };

            foreach (var vessel in sampleVessels)
            {
                context.Vessels.Add(vessel);
            }

            // Get MPAs for violation detection
            var mpas = await context.MarineProtectedAreas
                .Select(m => new { m.Id, m.Name, m.Boundary })
                .ToListAsync(ct);

            // Sample fishing event locations (mix of inside and outside MPAs)
            var fishingLocations = new[]
            {
                // Outside MPAs - legitimate fishing areas
                (Lon: -77.35, Lat: 24.75, Description: "West of Andros"),
                (Lon: -76.80, Lat: 25.10, Description: "Nassau Deep Water"),
                (Lon: -78.50, Lat: 26.50, Description: "Grand Bahama Banks"),
                (Lon: -77.00, Lat: 24.00, Description: "South Andros"),
                (Lon: -75.50, Lat: 23.75, Description: "Cat Island Area"),
                (Lon: -76.15, Lat: 24.05, Description: "Exuma Sound"),
                (Lon: -77.80, Lat: 25.80, Description: "Berry Islands"),
                (Lon: -73.50, Lat: 21.00, Description: "Great Inagua Waters"),
                (Lon: -77.10, Lat: 26.60, Description: "Abaco Banks"),
                (Lon: -74.85, Lat: 23.65, Description: "Long Island"),
                // Some locations that might be inside MPAs (potential violations)
                (Lon: -77.46, Lat: 24.31, Description: "Near Andros MPA"),
                (Lon: -75.78, Lat: 23.52, Description: "Near Exumas MPA"),
                (Lon: -77.33, Lat: 25.05, Description: "Central Bahamas")
            };

            var eventsCreated = 0;
            var now = DateTime.UtcNow;

            // Create fishing events spread over the last 30 days
            foreach (var vessel in sampleVessels)
            {
                var eventCount = random.Next(3, 8); // 3-7 events per vessel

                for (int i = 0; i < eventCount; i++)
                {
                    var location = fishingLocations[random.Next(fishingLocations.Length)];
                    var daysAgo = random.Next(1, 30);
                    var startTime = now.AddDays(-daysAgo).AddHours(random.Next(0, 24));
                    var durationHours = random.Next(2, 12) + random.NextDouble();
                    var endTime = startTime.AddHours(durationHours);

                    // Add jitter to coordinates so events don't stack exactly on top of each other
                    // Jitter of ~0.05 degrees is about 5km which is realistic for fishing operations
                    var lonJitter = (random.NextDouble() - 0.5) * 0.1; // +/- 0.05 degrees
                    var latJitter = (random.NextDouble() - 0.5) * 0.1; // +/- 0.05 degrees
                    var point = geometryFactory.CreatePoint(new Coordinate(location.Lon + lonJitter, location.Lat + latJitter));

                    var fishingEvent = VesselEvent.CreateFishingEvent(
                        vessel.Id,
                        point,
                        startTime,
                        endTime,
                        durationHours,
                        random.Next(5, 50) + random.NextDouble(), // Distance in km
                        $"SAMPLE-{Guid.NewGuid():N}"[..24]
                    );

                    // Check if inside any MPA
                    var insideMpa = mpas.FirstOrDefault(m => m.Boundary.Contains(point));
                    if (insideMpa != null)
                    {
                        fishingEvent.SetMpaContext(true, insideMpa.Id);
                    }
                    else
                    {
                        fishingEvent.SetMpaContext(false, null);
                    }

                    context.VesselEvents.Add(fishingEvent);
                    eventsCreated++;
                }
            }

            await context.SaveChangesAsync(ct);

            return Results.Ok(new
            {
                message = "Sample fishing data created successfully",
                vesselsCreated = sampleVessels.Length,
                eventsCreated
            });
        })
        .WithName("SeedFishingEvents")
        .WithDescription("Seed sample fishing events for development/demo (creates vessels and events)")
        .Produces<object>();

        // DELETE /api/admin/dev/clear-sample-data - Clear sample fishing data
        group.MapDelete("/dev/clear-sample-data", async (
            IMarineDbContext context,
            CancellationToken ct = default) =>
        {
            // Find sample vessels by name pattern
            var sampleVessels = await context.Vessels
                .Where(v => v.Name.StartsWith("F/V Sample") ||
                            v.Name.StartsWith("F/V Nassau Pride") ||
                            v.Name.StartsWith("F/V Caribbean Star") ||
                            v.Name.StartsWith("F/V Freeport Fisher") ||
                            v.Name.StartsWith("F/V Island Catch"))
                .ToListAsync(ct);

            if (!sampleVessels.Any())
            {
                return Results.Ok(new { message = "No sample data found to clear" });
            }

            var vesselIds = sampleVessels.Select(v => v.Id).ToList();

            // Delete related events first
            var events = await context.VesselEvents
                .Where(e => vesselIds.Contains(e.VesselId))
                .ToListAsync(ct);

            context.VesselEvents.RemoveRange(events);
            context.Vessels.RemoveRange(sampleVessels);

            await context.SaveChangesAsync(ct);

            return Results.Ok(new
            {
                message = "Sample data cleared",
                vesselsDeleted = sampleVessels.Count,
                eventsDeleted = events.Count
            });
        })
        .WithName("ClearSampleData")
        .WithDescription("Clear sample fishing data created for development")
        .Produces<object>();

        return endpoints;
    }
}

public record ApproveRequest(string? Notes);
public record RejectRequest(string Reason);
