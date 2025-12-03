using CoralLedger.Application.Common.Interfaces;
using CoralLedger.Application.Features.Bleaching.Queries.GetMpaBleachingHistory;
using MediatR;

namespace CoralLedger.Web.Endpoints;

public static class BleachingEndpoints
{
    public static IEndpointRouteBuilder MapBleachingEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/bleaching")
            .WithTags("Coral Bleaching");

        // GET /api/bleaching/point?lon=&lat=&date=
        group.MapGet("/point", async (
            ICoralReefWatchClient crwClient,
            double lon,
            double lat,
            DateOnly? date,
            CancellationToken ct = default) =>
        {
            var targetDate = date ?? DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
            var data = await crwClient.GetBleachingDataAsync(lon, lat, targetDate, ct);
            return data is null ? Results.NotFound() : Results.Ok(data);
        })
        .WithName("GetBleachingDataPoint")
        .WithDescription("Get coral bleaching heat stress data for a specific location from NOAA Coral Reef Watch")
        .Produces<CrwBleachingData>()
        .Produces(StatusCodes.Status404NotFound);

        // GET /api/bleaching/region?minLon=&minLat=&maxLon=&maxLat=&startDate=&endDate=
        group.MapGet("/region", async (
            ICoralReefWatchClient crwClient,
            double minLon,
            double minLat,
            double maxLon,
            double maxLat,
            DateOnly startDate,
            DateOnly endDate,
            CancellationToken ct = default) =>
        {
            var data = await crwClient.GetBleachingDataForRegionAsync(
                minLon, minLat, maxLon, maxLat, startDate, endDate, ct);
            return Results.Ok(data);
        })
        .WithName("GetBleachingDataRegion")
        .WithDescription("Get coral bleaching heat stress data for a geographic region from NOAA Coral Reef Watch")
        .Produces<IEnumerable<CrwBleachingData>>();

        // GET /api/bleaching/bahamas?date=
        group.MapGet("/bahamas", async (
            ICoralReefWatchClient crwClient,
            DateOnly? date,
            CancellationToken ct = default) =>
        {
            var data = await crwClient.GetBahamasBleachingAlertsAsync(date, ct);
            return Results.Ok(new BahamasBleachingResponse
            {
                Date = date ?? DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
                TotalDataPoints = data.Count(),
                AlertSummary = data
                    .GroupBy(d => d.AlertLevel)
                    .ToDictionary(g => GetAlertLevelName(g.Key), g => g.Count()),
                MaxDhw = data.Any() ? data.Max(d => d.DegreeHeatingWeek) : 0,
                AvgSst = data.Any() ? data.Average(d => d.SeaSurfaceTemperature) : 0,
                Data = data.Where(d => d.AlertLevel > 0).OrderByDescending(d => d.DegreeHeatingWeek).Take(100)
            });
        })
        .WithName("GetBahamasBleachingAlerts")
        .WithDescription("Get current coral bleaching alerts for the Bahamas from NOAA Coral Reef Watch")
        .Produces<BahamasBleachingResponse>();

        // GET /api/bleaching/timeseries?lon=&lat=&startDate=&endDate=
        group.MapGet("/timeseries", async (
            ICoralReefWatchClient crwClient,
            double lon,
            double lat,
            DateOnly startDate,
            DateOnly endDate,
            CancellationToken ct = default) =>
        {
            var data = await crwClient.GetBleachingTimeSeriesAsync(lon, lat, startDate, endDate, ct);
            return Results.Ok(data);
        })
        .WithName("GetBleachingTimeSeries")
        .WithDescription("Get bleaching heat stress time series for a specific location")
        .Produces<IEnumerable<CrwBleachingData>>();

        // GET /api/bleaching/mpa/{mpaId}?date= - Get bleaching data for a specific MPA
        group.MapGet("/mpa/{mpaId:guid}", async (
            Guid mpaId,
            ICoralReefWatchClient crwClient,
            IMarineDbContext dbContext,
            DateOnly? date,
            CancellationToken ct = default) =>
        {
            var mpa = await dbContext.MarineProtectedAreas.FindAsync(new object[] { mpaId }, ct);
            if (mpa is null)
                return Results.NotFound("MPA not found");

            var centroid = mpa.Centroid;
            var targetDate = date ?? DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
            var data = await crwClient.GetBleachingDataAsync(centroid.X, centroid.Y, targetDate, ct);

            if (data is null)
                return Results.NotFound("No bleaching data available");

            return Results.Ok(new MpaBleachingResponse
            {
                MpaId = mpaId,
                MpaName = mpa.Name,
                Date = targetDate,
                Data = data
            });
        })
        .WithName("GetMpaBleachingData")
        .WithDescription("Get bleaching heat stress data for a specific Marine Protected Area")
        .Produces<MpaBleachingResponse>()
        .Produces(StatusCodes.Status404NotFound);

        // GET /api/bleaching/mpa/{mpaId}/history?days=30 - Get historical bleaching data for an MPA
        group.MapGet("/mpa/{mpaId:guid}/history", async (
            Guid mpaId,
            IMediator mediator,
            int days = 30,
            CancellationToken ct = default) =>
        {
            var history = await mediator.Send(new GetMpaBleachingHistoryQuery(mpaId, days), ct);
            return Results.Ok(new MpaBleachingHistoryResponse
            {
                MpaId = mpaId,
                Days = days,
                DataPoints = history.Count,
                History = history
            });
        })
        .WithName("GetMpaBleachingHistory")
        .WithDescription("Get historical bleaching data from database for trend analysis")
        .Produces<MpaBleachingHistoryResponse>();

        return endpoints;
    }

    private static string GetAlertLevelName(int level) => level switch
    {
        0 => "NoStress",
        1 => "BleachingWatch",
        2 => "BleachingWarning",
        3 => "AlertLevel1",
        4 => "AlertLevel2",
        5 => "AlertLevel3",
        6 => "AlertLevel4",
        7 => "AlertLevel5",
        _ => $"Unknown_{level}"
    };
}

public record BahamasBleachingResponse
{
    public DateOnly Date { get; init; }
    public int TotalDataPoints { get; init; }
    public Dictionary<string, int> AlertSummary { get; init; } = new();
    public double MaxDhw { get; init; }
    public double AvgSst { get; init; }
    public IEnumerable<CrwBleachingData> Data { get; init; } = Enumerable.Empty<CrwBleachingData>();
}

public record MpaBleachingResponse
{
    public Guid MpaId { get; init; }
    public string MpaName { get; init; } = string.Empty;
    public DateOnly Date { get; init; }
    public CrwBleachingData? Data { get; init; }
}

public record MpaBleachingHistoryResponse
{
    public Guid MpaId { get; init; }
    public int Days { get; init; }
    public int DataPoints { get; init; }
    public IReadOnlyList<BleachingHistoryDto> History { get; init; } = [];
}
