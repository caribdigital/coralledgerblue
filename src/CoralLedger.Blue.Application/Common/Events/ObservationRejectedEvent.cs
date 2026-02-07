using MediatR;

namespace CoralLedger.Blue.Application.Common.Events;

/// <summary>
/// Published when an observation is rejected
/// </summary>
public record ObservationRejectedEvent(
    Guid ObservationId,
    string CitizenEmail,
    string Reason
) : INotification;
