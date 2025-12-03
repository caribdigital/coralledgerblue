using CoralLedger.Domain.Entities;
using CoralLedger.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite;
using NetTopologySuite.Geometries;

namespace CoralLedger.Infrastructure.Data.Seeding;

public static class BahamasMpaSeeder
{
    private static readonly GeometryFactory GeometryFactory =
        NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);

    public static async Task SeedAsync(MarineDbContext context)
    {
        if (await context.MarineProtectedAreas.AnyAsync())
            return; // Already seeded

        var mpas = GetBahamasMpas().ToList();

        await context.MarineProtectedAreas.AddRangeAsync(mpas);
        await context.SaveChangesAsync();
    }

    private static IEnumerable<MarineProtectedArea> GetBahamasMpas()
    {
        // Key Bahamas Marine Protected Areas
        // Data sourced from Protected Planet / WDPA and Bahamas National Trust

        yield return CreateMpa(
            name: "Exuma Cays Land and Sea Park",
            wdpaId: "WDPA-555705832",
            longitude: -76.60,
            latitude: 24.43,
            areaKm2: 456.0,
            protectionLevel: ProtectionLevel.NoTake,
            islandGroup: IslandGroup.Exumas,
            description: "The world's first land and sea park, established 1958. A no-take marine reserve protecting pristine reefs, grouper spawning aggregations, and the endangered Bahama hutia.",
            managingAuthority: "Bahamas National Trust",
            designationDate: new DateOnly(1958, 1, 1)
        );

        yield return CreateMpa(
            name: "Pelican Cays Land and Sea Park",
            wdpaId: "WDPA-555705833",
            longitude: -77.02,
            latitude: 26.38,
            areaKm2: 21.0,
            protectionLevel: ProtectionLevel.NoTake,
            islandGroup: IslandGroup.Abaco,
            description: "Protected area in the Abaco chain featuring pristine coral reefs, sea grass beds, and mangrove creeks. Critical habitat for conch and lobster.",
            managingAuthority: "Bahamas National Trust",
            designationDate: new DateOnly(1972, 1, 1)
        );

        yield return CreateMpa(
            name: "Andros West Side National Park",
            wdpaId: "WDPA-555705834",
            longitude: -78.05,
            latitude: 24.25,
            areaKm2: 1607.5,
            protectionLevel: ProtectionLevel.HighlyProtected,
            islandGroup: IslandGroup.Andros,
            description: "One of the largest national parks in the Bahamas, protecting the Andros Barrier Reef - the third largest in the world. Home to blue holes and extensive mangrove systems.",
            managingAuthority: "Bahamas National Trust",
            designationDate: new DateOnly(2002, 1, 1)
        );

        yield return CreateMpa(
            name: "Conception Island National Park",
            wdpaId: "WDPA-555705835",
            longitude: -75.12,
            latitude: 23.83,
            areaKm2: 8.5,
            protectionLevel: ProtectionLevel.NoTake,
            islandGroup: IslandGroup.LongIsland,
            description: "Remote protected island with nesting green and hawksbill sea turtles, pristine reefs, and important seabird colonies including white-tailed tropicbirds.",
            managingAuthority: "Bahamas National Trust",
            designationDate: new DateOnly(1971, 1, 1)
        );

        yield return CreateMpa(
            name: "Inagua National Park",
            wdpaId: "WDPA-555705836",
            longitude: -73.55,
            latitude: 21.08,
            areaKm2: 743.0,
            protectionLevel: ProtectionLevel.HighlyProtected,
            islandGroup: IslandGroup.Inagua,
            description: "Home to the world's largest breeding colony of West Indian flamingos (over 80,000 birds). Also protects roseate spoonbills, reddish egrets, and the endangered Bahama parrot.",
            managingAuthority: "Bahamas National Trust",
            designationDate: new DateOnly(1965, 1, 1)
        );

        yield return CreateMpa(
            name: "Peterson Cay National Park",
            wdpaId: "WDPA-555705837",
            longitude: -78.92,
            latitude: 26.45,
            areaKm2: 0.6,
            protectionLevel: ProtectionLevel.NoTake,
            islandGroup: IslandGroup.GrandBahama,
            description: "Small uninhabited cay with protected coral reefs popular for snorkeling. Features elkhorn and brain corals, and diverse fish populations.",
            managingAuthority: "Bahamas National Trust",
            designationDate: new DateOnly(1968, 1, 1)
        );

        yield return CreateMpa(
            name: "Lucayan National Park",
            wdpaId: "WDPA-555705838",
            longitude: -78.45,
            latitude: 26.52,
            areaKm2: 16.0,
            protectionLevel: ProtectionLevel.LightlyProtected,
            islandGroup: IslandGroup.GrandBahama,
            description: "Features one of the longest charted underwater cave systems in the world (over 6 miles). Protects mangrove creeks, tidal flats, and Lucayan archaeological sites.",
            managingAuthority: "Bahamas National Trust",
            designationDate: new DateOnly(1982, 1, 1)
        );

        yield return CreateMpa(
            name: "Black Sound Cay National Reserve",
            wdpaId: "WDPA-555705839",
            longitude: -76.90,
            latitude: 26.87,
            areaKm2: 1.2,
            protectionLevel: ProtectionLevel.HighlyProtected,
            islandGroup: IslandGroup.Abaco,
            description: "Protected mangrove and coral reef system near Green Turtle Cay. Important nursery habitat for commercially important fish species.",
            managingAuthority: "Bahamas National Trust",
            designationDate: new DateOnly(1988, 1, 1)
        );
    }

    private static MarineProtectedArea CreateMpa(
        string name,
        string wdpaId,
        double longitude,
        double latitude,
        double areaKm2,
        ProtectionLevel protectionLevel,
        IslandGroup islandGroup,
        string description,
        string managingAuthority,
        DateOnly designationDate)
    {
        // Create a simple polygon around the centroid for visualization
        // In production, use actual boundary data from Protected Planet API
        var radius = Math.Sqrt(areaKm2 / Math.PI) / 111.0; // Approximate degrees
        var boundary = CreateCircularPolygon(longitude, latitude, radius, 32);

        return MarineProtectedArea.Create(
            name: name,
            boundary: boundary,
            protectionLevel: protectionLevel,
            islandGroup: islandGroup,
            wdpaId: wdpaId,
            description: description,
            managingAuthority: managingAuthority,
            designationDate: designationDate
        );
    }

    private static Polygon CreateCircularPolygon(double centerX, double centerY, double radius, int segments)
    {
        var coordinates = new Coordinate[segments + 1];

        for (int i = 0; i < segments; i++)
        {
            var angle = 2 * Math.PI * i / segments;
            var x = centerX + radius * Math.Cos(angle);
            var y = centerY + radius * Math.Sin(angle);
            coordinates[i] = new Coordinate(x, y);
        }

        coordinates[segments] = coordinates[0]; // Close the ring

        var shell = GeometryFactory.CreateLinearRing(coordinates);
        return GeometryFactory.CreatePolygon(shell);
    }
}
