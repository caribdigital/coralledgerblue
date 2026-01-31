using CoralLedger.Blue.Application.Common.Interfaces;
using CoralLedger.Blue.Application.Common.Models;

namespace CoralLedger.Blue.Web.Endpoints;

public static class AisEndpoints
{
    public static IEndpointRouteBuilder MapAisEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/ais")
            .WithTags("AIS Vessel Tracking");

        // GET /api/ais/status - Check if AIS is configured
        group.MapGet("/status", (IAisClient aisClient) =>
        {
            return Results.Ok(new
            {
                configured = aisClient.IsConfigured,
                message = aisClient.IsConfigured
                    ? "AIS tracking is active"
                    : "AIS tracking is not configured. Demo data will be used."
            });
        })
        .WithName("GetAisStatus")
        .Produces<object>();

        // GET /api/ais/vessels - Get current vessel positions
        group.MapGet("/vessels", async (
            IAisClient aisClient,
            CancellationToken ct = default) =>
        {
            var result = await aisClient.GetVesselPositionsAsync(ct).ConfigureAwait(false);

            if (!result.Success)
            {
                return Results.Problem(
                    detail: result.ErrorMessage,
                    statusCode: 500,
                    title: "Failed to fetch vessel positions");
            }

            var vessels = result.Value ?? Array.Empty<AisVesselPosition>();

            return Results.Ok(new
            {
                count = vessels.Count,
                isDemo = !aisClient.IsConfigured,
                timestamp = DateTime.UtcNow,
                vessels = vessels.Select(v => new
                {
                    v.Mmsi,
                    v.Name,
                    v.Longitude,
                    v.Latitude,
                    v.Speed,
                    v.Course,
                    v.Heading,
                    v.VesselType,
                    v.Flag,
                    v.Destination,
                    v.Timestamp
                })
            });
        })
        .WithName("GetAisVessels")
        .Produces<object>();

        // GET /api/ais/vessels/near - Get vessels near a location
        group.MapGet("/vessels/near", async (
            double lon,
            double lat,
            double radiusKm,
            IAisClient aisClient,
            CancellationToken ct = default) =>
        {
            var result = await aisClient.GetVesselPositionsNearAsync(lon, lat, radiusKm, ct).ConfigureAwait(false);

            if (!result.Success)
            {
                return Results.Problem(
                    detail: result.ErrorMessage,
                    statusCode: 500,
                    title: "Failed to fetch nearby vessels");
            }

            var vessels = result.Value ?? Array.Empty<AisVesselPosition>();

            return Results.Ok(new
            {
                count = vessels.Count,
                center = new { lon, lat },
                radiusKm,
                vessels = vessels.Select(v => new
                {
                    v.Mmsi,
                    v.Name,
                    v.Longitude,
                    v.Latitude,
                    v.Speed,
                    v.Course,
                    v.VesselType,
                    v.Timestamp
                })
            });
        })
        .WithName("GetAisVesselsNear")
        .Produces<object>();

        // GET /api/ais/vessels/{mmsi}/track - Get vessel track history
        group.MapGet("/vessels/{mmsi}/track", async (
            string mmsi,
            int hours,
            IAisClient aisClient,
            CancellationToken ct = default) =>
        {
            var result = await aisClient.GetVesselTrackAsync(mmsi, hours, ct).ConfigureAwait(false);

            if (!result.Success)
            {
                return Results.Problem(
                    detail: result.ErrorMessage,
                    statusCode: 500,
                    title: "Failed to fetch vessel track");
            }

            var track = result.Value ?? Array.Empty<AisVesselPosition>();

            return Results.Ok(new
            {
                mmsi,
                hours,
                pointCount = track.Count,
                track = track.Select(p => new
                {
                    p.Longitude,
                    p.Latitude,
                    p.Speed,
                    p.Course,
                    p.Timestamp
                })
            });
        })
        .WithName("GetAisVesselTrack")
        .Produces<object>();

        return endpoints;
    }
}
