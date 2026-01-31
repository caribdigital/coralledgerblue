namespace CoralLedger.Blue.Application.Common.Interfaces;

/// <summary>
/// Service for generating PDF reports for MPAs
/// </summary>
public interface IReportGenerationService
{
    /// <summary>
    /// Generate a detailed PDF report for a single MPA
    /// </summary>
    /// <param name="mpaId">The MPA ID</param>
    /// <param name="options">Report generation options including date range</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>PDF file as byte array</returns>
    Task<byte[]> GenerateMpaReportAsync(Guid mpaId, ReportOptions? options = null, CancellationToken ct = default);

    /// <summary>
    /// Generate a summary PDF report for all MPAs
    /// </summary>
    /// <param name="options">Report generation options including date range and filters</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>PDF file as byte array</returns>
    Task<byte[]> GenerateAllMpasReportAsync(ReportOptions? options = null, CancellationToken ct = default);
}

/// <summary>
/// Options for generating reports
/// </summary>
public class ReportOptions
{
    /// <summary>
    /// Start date for data filtering
    /// </summary>
    public DateTime? FromDate { get; set; }

    /// <summary>
    /// End date for data filtering
    /// </summary>
    public DateTime? ToDate { get; set; }

    /// <summary>
    /// Filter by specific MPA IDs (for all-MPAs report)
    /// </summary>
    public List<Guid>? MpaIds { get; set; }

    /// <summary>
    /// Filter by island group
    /// </summary>
    public string? IslandGroup { get; set; }

    /// <summary>
    /// Filter by protection level
    /// </summary>
    public string? ProtectionLevel { get; set; }

    /// <summary>
    /// Include detailed charts and maps
    /// </summary>
    public bool IncludeCharts { get; set; } = true;

    /// <summary>
    /// Include observation photos (may increase file size)
    /// </summary>
    public bool IncludePhotos { get; set; } = false;
}
