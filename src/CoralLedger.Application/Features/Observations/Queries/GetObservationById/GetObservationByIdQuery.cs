using CoralLedger.Application.Common.Interfaces;
using CoralLedger.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CoralLedger.Application.Features.Observations.Queries.GetObservationById;

public record GetObservationByIdQuery(Guid Id) : IRequest<ObservationDetailDto?>;

public record ObservationDetailDto(
    Guid Id,
    double Longitude,
    double Latitude,
    DateTime ObservationTime,
    string Title,
    string? Description,
    ObservationType Type,
    int Severity,
    ObservationStatus Status,
    string? ModerationNotes,
    DateTime? ModeratedAt,
    bool? IsInMpa,
    string? MpaName,
    Guid? MpaId,
    string? ReefName,
    Guid? ReefId,
    string? CitizenEmail,
    string? CitizenName,
    IReadOnlyList<PhotoDto> Photos,
    DateTime CreatedAt);

public record PhotoDto(
    Guid Id,
    string BlobUri,
    string? Caption,
    int DisplayOrder,
    DateTime UploadedAt);

public class GetObservationByIdQueryHandler : IRequestHandler<GetObservationByIdQuery, ObservationDetailDto?>
{
    private readonly IMarineDbContext _context;

    public GetObservationByIdQueryHandler(IMarineDbContext context)
    {
        _context = context;
    }

    public async Task<ObservationDetailDto?> Handle(
        GetObservationByIdQuery request,
        CancellationToken cancellationToken)
    {
        var observation = await _context.CitizenObservations
            .AsNoTracking()
            .Include(o => o.MarineProtectedArea)
            .Include(o => o.Reef)
            .Include(o => o.Photos)
            .Where(o => o.Id == request.Id)
            .Select(o => new ObservationDetailDto(
                o.Id,
                o.Location.X,
                o.Location.Y,
                o.ObservationTime,
                o.Title,
                o.Description,
                o.Type,
                o.Severity,
                o.Status,
                o.ModerationNotes,
                o.ModeratedAt,
                o.IsInMpa,
                o.MarineProtectedArea != null ? o.MarineProtectedArea.Name : null,
                o.MarineProtectedAreaId,
                o.Reef != null ? o.Reef.Name : null,
                o.ReefId,
                o.CitizenEmail,
                o.CitizenName,
                o.Photos
                    .OrderBy(p => p.DisplayOrder)
                    .Select(p => new PhotoDto(
                        p.Id,
                        p.BlobUri,
                        p.Caption,
                        p.DisplayOrder,
                        p.UploadedAt))
                    .ToList(),
                o.CreatedAt))
            .FirstOrDefaultAsync(cancellationToken);

        return observation;
    }
}
