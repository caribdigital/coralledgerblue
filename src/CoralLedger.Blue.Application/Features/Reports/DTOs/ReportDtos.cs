namespace CoralLedger.Blue.Application.Features.Reports.DTOs;

/// <summary>
/// DTO for MPA status report data
/// </summary>
public record MpaStatusReportDto
{
    public Guid MpaId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? LocalName { get; init; }
    public double AreaSquareKm { get; init; }
    public string Status { get; init; } = string.Empty;
    public string ProtectionLevel { get; init; } = string.Empty;
    public string IslandGroup { get; init; } = string.Empty;
    public DateOnly? DesignationDate { get; init; }
    public string? ManagingAuthority { get; init; }
    public string? Description { get; init; }
    public double CentroidLongitude { get; init; }
    public double CentroidLatitude { get; init; }
    public int ReefCount { get; init; }

    // Bleaching data summary
    public BleachingDataSummary BleachingData { get; init; } = new();

    // Fishing activity summary
    public FishingActivitySummary FishingActivity { get; init; } = new();

    // Observations summary
    public ObservationsSummary Observations { get; init; } = new();

    // Report metadata
    public DateTime GeneratedAt { get; init; }
    public DateTime? DataFromDate { get; init; }
    public DateTime? DataToDate { get; init; }
}

/// <summary>
/// Summary of bleaching data for an MPA
/// </summary>
public record BleachingDataSummary
{
    public int TotalAlerts { get; init; }
    public double? MaxDegreeHeatingWeeks { get; init; }
    public double? AvgSeaSurfaceTemp { get; init; }
    public double? MaxSeaSurfaceTemp { get; init; }
    public int CriticalAlertsCount { get; init; }
    public DateTime? LastAlertDate { get; init; }
    public List<BleachingAlertItem> RecentAlerts { get; init; } = new();
}

public record BleachingAlertItem
{
    public DateTime Date { get; init; }
    public double DegreeHeatingWeeks { get; init; }
    public double SeaSurfaceTemp { get; init; }
    public string AlertLevel { get; init; } = string.Empty;
}

/// <summary>
/// Summary of fishing activity for an MPA
/// </summary>
public record FishingActivitySummary
{
    public int TotalVesselEvents { get; init; }
    public int FishingEvents { get; init; }
    public int PortVisits { get; init; }
    public int Encounters { get; init; }
    public int UniqueVessels { get; init; }
    public DateTime? LastActivityDate { get; init; }
    public List<VesselEventItem> RecentEvents { get; init; } = new();
    public Dictionary<string, int> EventsByType { get; init; } = new();
}

public record VesselEventItem
{
    public DateTime StartTime { get; init; }
    public string EventType { get; init; } = string.Empty;
    public string VesselName { get; init; } = string.Empty;
    public string? VesselMmsi { get; init; }
    public double? DurationHours { get; init; }
}

/// <summary>
/// Summary of citizen observations for an MPA
/// </summary>
public record ObservationsSummary
{
    public int TotalObservations { get; init; }
    public int ApprovedObservations { get; init; }
    public int PendingObservations { get; init; }
    public int RejectedObservations { get; init; }
    public double? AvgSeverity { get; init; }
    public DateTime? LastObservationDate { get; init; }
    public List<ObservationItem> RecentObservations { get; init; } = new();
    public Dictionary<int, int> ObservationsBySeverity { get; init; } = new();
}

public record ObservationItem
{
    public DateTime ObservedAt { get; init; }
    public string Description { get; init; } = string.Empty;
    public int Severity { get; init; }
    public string Status { get; init; } = string.Empty;
    public string? ObserverName { get; init; }
}

/// <summary>
/// DTO for all-MPAs summary report
/// </summary>
public record AllMpasSummaryReportDto
{
    public int TotalMpas { get; init; }
    public double TotalAreaSquareKm { get; init; }
    public List<MpaSummaryItem> Mpas { get; init; } = new();
    public OverallStatistics Statistics { get; init; } = new();
    public DateTime GeneratedAt { get; init; }
    public DateTime? DataFromDate { get; init; }
    public DateTime? DataToDate { get; init; }
}

public record MpaSummaryItem
{
    public Guid MpaId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string IslandGroup { get; init; } = string.Empty;
    public string ProtectionLevel { get; init; } = string.Empty;
    public double AreaSquareKm { get; init; }
    public int TotalAlerts { get; init; }
    public int TotalVesselEvents { get; init; }
    public int TotalObservations { get; init; }
    public string Status { get; init; } = string.Empty;
}

public record OverallStatistics
{
    public int TotalBleachingAlerts { get; init; }
    public int TotalVesselEvents { get; init; }
    public int TotalObservations { get; init; }
    public int ActiveMpas { get; init; }
    public int DecommissionedMpas { get; init; }
    public Dictionary<string, int> MpasByIslandGroup { get; init; } = new();
    public Dictionary<string, int> MpasByProtectionLevel { get; init; } = new();
}
