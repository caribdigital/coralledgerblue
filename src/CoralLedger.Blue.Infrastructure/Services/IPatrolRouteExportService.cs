using CoralLedger.Blue.Domain.Entities;

namespace CoralLedger.Blue.Infrastructure.Services.PatrolExport;

public interface IPatrolRouteExportService
{
    /// <summary>
    /// Export patrol route as GPX format (GPS Exchange Format)
    /// </summary>
    string ExportToGpx(PatrolRoute patrolRoute);

    /// <summary>
    /// Export patrol route as KML format (Keyhole Markup Language)
    /// </summary>
    string ExportToKml(PatrolRoute patrolRoute);
}
