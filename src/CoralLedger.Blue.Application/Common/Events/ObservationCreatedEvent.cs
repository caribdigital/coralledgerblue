using CoralLedger.Blue.Domain.Enums;
using MediatR;

namespace CoralLedger.Blue.Application.Common.Events;

/// <summary>
/// Published when a new citizen observation is created
/// </summary>
public record ObservationCreatedEvent(
    Guid ObservationId,
    string? CitizenEmail,
    ObservationType Type,
    bool HasPhotos,
    bool HasLocation,
    bool IsInMpa
) : INotification;
