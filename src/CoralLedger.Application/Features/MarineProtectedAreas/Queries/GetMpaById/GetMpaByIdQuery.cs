using CoralLedger.Application.Common.Interfaces;
using CoralLedger.Application.Features.MarineProtectedAreas.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.IO;

namespace CoralLedger.Application.Features.MarineProtectedAreas.Queries.GetMpaById;

public record GetMpaByIdQuery(Guid Id) : IRequest<MpaDetailDto?>;

public class GetMpaByIdQueryHandler : IRequestHandler<GetMpaByIdQuery, MpaDetailDto?>
{
    private readonly IMarineDbContext _context;

    public GetMpaByIdQueryHandler(IMarineDbContext context)
    {
        _context = context;
    }

    public async Task<MpaDetailDto?> Handle(
        GetMpaByIdQuery request,
        CancellationToken cancellationToken)
    {
        var mpa = await _context.MarineProtectedAreas
            .AsNoTracking()
            .Include(m => m.Reefs)
            .FirstOrDefaultAsync(m => m.Id == request.Id, cancellationToken);

        if (mpa is null)
            return null;

        var geoJsonWriter = new GeoJsonWriter();

        return new MpaDetailDto
        {
            Id = mpa.Id,
            Name = mpa.Name,
            LocalName = mpa.LocalName,
            WdpaId = mpa.WdpaId,
            AreaSquareKm = mpa.AreaSquareKm,
            Status = mpa.Status.ToString(),
            ProtectionLevel = mpa.ProtectionLevel.ToString(),
            IslandGroup = mpa.IslandGroup.ToString(),
            DesignationDate = mpa.DesignationDate,
            ManagingAuthority = mpa.ManagingAuthority,
            Description = mpa.Description,
            CentroidLongitude = mpa.Centroid.X,
            CentroidLatitude = mpa.Centroid.Y,
            BoundaryGeoJson = geoJsonWriter.Write(mpa.Boundary),
            ReefCount = mpa.Reefs.Count
        };
    }
}
