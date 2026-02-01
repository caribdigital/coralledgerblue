namespace CoralLedger.Blue.Domain.Enums;

/// <summary>
/// Status of a patrol route recording
/// </summary>
public enum PatrolRouteStatus
{
    /// <summary>
    /// Patrol is currently in progress and recording GPS points
    /// </summary>
    InProgress,

    /// <summary>
    /// Patrol has been completed and stopped
    /// </summary>
    Completed,

    /// <summary>
    /// Patrol was cancelled or abandoned
    /// </summary>
    Cancelled
}
