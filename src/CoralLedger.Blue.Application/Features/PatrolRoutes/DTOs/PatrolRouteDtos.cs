namespace CoralLedger.Blue.Application.Features.PatrolRoutes.DTOs;

public record PatrolRouteSummaryDto(
    Guid Id,
    string? OfficerName,
    string? OfficerId,
    DateTime StartTime,
    DateTime? EndTime,
    string Status,
    double? TotalDistanceMeters,
    int? DurationSeconds,
    int PointCount,
    int WaypointCount,
    string? MpaName);

public record PatrolRouteDetailDto(
    Guid Id,
    string? OfficerName,
    string? OfficerId,
    DateTime StartTime,
    DateTime? EndTime,
    string Status,
    string? Notes,
    int RecordingIntervalSeconds,
    double? TotalDistanceMeters,
    int? DurationSeconds,
    Guid? MarineProtectedAreaId,
    string? MpaName,
    List<PatrolRoutePointDto> Points,
    List<PatrolWaypointDto> Waypoints);

public record PatrolRoutePointDto(
    Guid Id,
    double Longitude,
    double Latitude,
    DateTime Timestamp,
    double? Accuracy,
    double? Altitude,
    double? Speed,
    double? Heading);

public record PatrolWaypointDto(
    Guid Id,
    double Longitude,
    double Latitude,
    DateTime Timestamp,
    string Title,
    string? Notes,
    string? WaypointType);
