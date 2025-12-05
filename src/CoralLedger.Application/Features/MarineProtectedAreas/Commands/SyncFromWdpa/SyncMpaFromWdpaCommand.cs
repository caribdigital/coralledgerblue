using CoralLedger.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging;

namespace CoralLedger.Application.Features.MarineProtectedAreas.Commands.SyncFromWdpa;

/// <summary>
/// Command to sync an MPA's boundary from the Protected Planet WDPA API
/// </summary>
public record SyncMpaFromWdpaCommand(Guid MpaId) : IRequest<SyncResult>;

/// <summary>
/// Result of the WDPA sync operation
/// </summary>
public record SyncResult(
    bool Success,
    string? Error = null,
    DateTime? SyncedAt = null,
    double? NewAreaKm2 = null);

public class SyncMpaFromWdpaCommandHandler : IRequestHandler<SyncMpaFromWdpaCommand, SyncResult>
{
    private readonly IMarineDbContext _context;
    private readonly IProtectedPlanetClient _protectedPlanetClient;
    private readonly ISpatialValidationService _spatialValidation;
    private readonly ICacheService _cache;
    private readonly ILogger<SyncMpaFromWdpaCommandHandler> _logger;

    public SyncMpaFromWdpaCommandHandler(
        IMarineDbContext context,
        IProtectedPlanetClient protectedPlanetClient,
        ISpatialValidationService spatialValidation,
        ICacheService cache,
        ILogger<SyncMpaFromWdpaCommandHandler> logger)
    {
        _context = context;
        _protectedPlanetClient = protectedPlanetClient;
        _spatialValidation = spatialValidation;
        _cache = cache;
        _logger = logger;
    }

    public async Task<SyncResult> Handle(SyncMpaFromWdpaCommand request, CancellationToken cancellationToken)
    {
        // Check if API is configured
        if (!_protectedPlanetClient.IsConfigured)
        {
            _logger.LogWarning("Protected Planet API is not configured. Cannot sync MPA {MpaId}", request.MpaId);
            return new SyncResult(false, "Protected Planet API token not configured");
        }

        // Find the MPA
        var mpa = await _context.MarineProtectedAreas
            .FirstOrDefaultAsync(m => m.Id == request.MpaId, cancellationToken);

        if (mpa == null)
        {
            _logger.LogWarning("MPA not found: {MpaId}", request.MpaId);
            return new SyncResult(false, $"MPA not found: {request.MpaId}");
        }

        // Check if MPA has a WDPA ID
        if (string.IsNullOrEmpty(mpa.WdpaId))
        {
            _logger.LogWarning("MPA {MpaId} does not have a WDPA ID", request.MpaId);
            return new SyncResult(false, "MPA does not have a WDPA ID configured");
        }

        _logger.LogInformation("Syncing MPA {MpaName} from WDPA ID {WdpaId}", mpa.Name, mpa.WdpaId);

        // Fetch from Protected Planet API
        var protectedArea = await _protectedPlanetClient.GetProtectedAreaAsync(
            mpa.WdpaId,
            withGeometry: true,
            cancellationToken);

        if (protectedArea == null)
        {
            _logger.LogWarning("Protected area not found in WDPA: {WdpaId}", mpa.WdpaId);
            return new SyncResult(false, $"Protected area not found in WDPA: {mpa.WdpaId}");
        }

        if (protectedArea.Boundary == null)
        {
            _logger.LogWarning("Protected area {WdpaId} has no boundary geometry", mpa.WdpaId);
            return new SyncResult(false, "Protected area has no boundary geometry in WDPA");
        }

        // Validate the geometry
        var validationResult = _spatialValidation.ValidateGeometry(protectedArea.Boundary);
        if (!validationResult.IsValid)
        {
            var gates = string.Join(", ", validationResult.FailedGates);
            var errors = string.Join("; ", validationResult.Errors);
            _logger.LogWarning("Invalid geometry from WDPA for {WdpaId} (gates: {Gates}): {Errors}", mpa.WdpaId, gates, errors);
            return new SyncResult(false, $"Invalid geometry (gates: {gates}): {errors}");
        }

        // Update the MPA boundary
        mpa.UpdateBoundaryFromWdpa(protectedArea.Boundary);

        // Save changes first
        await _context.SaveChangesAsync(cancellationToken);

        // Generate simplified geometries using PostGIS
        await GenerateSimplifiedGeometriesAsync(mpa.Id, cancellationToken);

        // Invalidate all MPA-related cache entries
        await InvalidateMpaCacheAsync(mpa.Id, cancellationToken);

        _logger.LogInformation(
            "Successfully synced MPA {MpaName} from WDPA. New area: {AreaKm2:F2} km²",
            mpa.Name, mpa.AreaSquareKm);

        return new SyncResult(
            Success: true,
            SyncedAt: mpa.WdpaLastSync,
            NewAreaKm2: mpa.AreaSquareKm);
    }

    private async Task GenerateSimplifiedGeometriesAsync(Guid mpaId, CancellationToken cancellationToken)
    {
        try
        {
            // Use PostGIS ST_SimplifyPreserveTopology to generate simplified versions
            // Medium: 0.001° tolerance (~100m at 25°N latitude)
            // Low: 0.01° tolerance (~1km at 25°N latitude)
            var sql = @"
                UPDATE marine_protected_areas
                SET
                    ""BoundarySimplifiedMedium"" = ST_SimplifyPreserveTopology(""Boundary"", 0.001),
                    ""BoundarySimplifiedLow"" = ST_SimplifyPreserveTopology(""Boundary"", 0.01)
                WHERE ""Id"" = {0}";

            await _context.Database.ExecuteSqlRawAsync(sql, [mpaId], cancellationToken);

            _logger.LogDebug("Generated simplified geometries for MPA {MpaId}", mpaId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate simplified geometries for MPA {MpaId}", mpaId);
            // Don't fail the whole sync if simplification fails
        }
    }

    private async Task InvalidateMpaCacheAsync(Guid mpaId, CancellationToken cancellationToken)
    {
        try
        {
            // Invalidate all MPA GeoJSON cache entries (all resolutions)
            await _cache.RemoveByPrefixAsync(CacheKeys.MpaPrefix, cancellationToken);

            // Also invalidate specific MPA detail cache
            await _cache.RemoveAsync(CacheKeys.ForMpa(mpaId), cancellationToken);

            _logger.LogDebug("Invalidated cache for MPA {MpaId}", mpaId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to invalidate cache for MPA {MpaId}", mpaId);
            // Don't fail the whole sync if cache invalidation fails
        }
    }
}
