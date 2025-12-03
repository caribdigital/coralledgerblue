using CoralLedger.Application.Common.Interfaces;
using CoralLedger.Application.Features.MarineProtectedAreas.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CoralLedger.Application.Features.MarineProtectedAreas.Queries.GetAllMpas;

public record GetAllMpasQuery : IRequest<IReadOnlyList<MpaSummaryDto>>;

public class GetAllMpasQueryHandler : IRequestHandler<GetAllMpasQuery, IReadOnlyList<MpaSummaryDto>>
{
    private readonly IMarineDbContext _context;

    public GetAllMpasQueryHandler(IMarineDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<MpaSummaryDto>> Handle(
        GetAllMpasQuery request,
        CancellationToken cancellationToken)
    {
        return await _context.MarineProtectedAreas
            .AsNoTracking()
            .Select(mpa => new MpaSummaryDto
            {
                Id = mpa.Id,
                Name = mpa.Name,
                AreaSquareKm = mpa.AreaSquareKm,
                ProtectionLevel = mpa.ProtectionLevel.ToString(),
                IslandGroup = mpa.IslandGroup.ToString(),
                CentroidLongitude = mpa.Centroid.X,
                CentroidLatitude = mpa.Centroid.Y
            })
            .ToListAsync(cancellationToken);
    }
}
