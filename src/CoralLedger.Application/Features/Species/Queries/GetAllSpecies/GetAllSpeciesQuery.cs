using CoralLedger.Application.Common.Interfaces;
using CoralLedger.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CoralLedger.Application.Features.Species.Queries.GetAllSpecies;

public record GetAllSpeciesQuery : IRequest<IReadOnlyList<SpeciesDto>>;

public record SpeciesDto(
    Guid Id,
    string ScientificName,
    string CommonName,
    string? LocalName,
    string Category,
    string ConservationStatus,
    bool IsInvasive,
    bool IsThreatened,
    string? Description,
    string? IdentificationTips,
    string? Habitat,
    int? TypicalDepthMinM,
    int? TypicalDepthMaxM);

public class GetAllSpeciesQueryHandler : IRequestHandler<GetAllSpeciesQuery, IReadOnlyList<SpeciesDto>>
{
    private readonly IMarineDbContext _context;

    public GetAllSpeciesQueryHandler(IMarineDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<SpeciesDto>> Handle(
        GetAllSpeciesQuery request,
        CancellationToken cancellationToken)
    {
        return await _context.BahamianSpecies
            .OrderBy(s => s.Category)
            .ThenBy(s => s.CommonName)
            .Select(s => new SpeciesDto(
                s.Id,
                s.ScientificName,
                s.CommonName,
                s.LocalName,
                s.Category.ToString(),
                s.ConservationStatus.ToString(),
                s.IsInvasive,
                s.IsThreatened,
                s.Description,
                s.IdentificationTips,
                s.Habitat,
                s.TypicalDepthMinM,
                s.TypicalDepthMaxM))
            .ToListAsync(cancellationToken);
    }
}
