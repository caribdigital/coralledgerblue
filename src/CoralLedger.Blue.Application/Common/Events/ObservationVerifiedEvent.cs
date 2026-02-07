using MediatR;

namespace CoralLedger.Blue.Application.Common.Events;

/// <summary>
/// Published when an observation is approved/verified
/// </summary>
public record ObservationVerifiedEvent(
    Guid ObservationId,
    string CitizenEmail,
    int PointsAwarded
) : INotification;
