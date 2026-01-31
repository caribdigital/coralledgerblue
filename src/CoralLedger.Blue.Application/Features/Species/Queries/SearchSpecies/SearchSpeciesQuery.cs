using CoralLedger.Blue.Application.Common.Interfaces;
using CoralLedger.Blue.Application.Features.Species.Queries.GetAllSpecies;
using CoralLedger.Blue.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CoralLedger.Blue.Application.Features.Species.Queries.SearchSpecies;

public record SearchSpeciesQuery(
    string? SearchTerm = null,
    SpeciesCategory? Category = null,
    bool? IsInvasive = null,
    bool? IsThreatened = null) : IRequest<IReadOnlyList<SpeciesDto>>;

public class SearchSpeciesQueryHandler : IRequestHandler<SearchSpeciesQuery, IReadOnlyList<SpeciesDto>>
{
    private readonly IMarineDbContext _context;

    public SearchSpeciesQueryHandler(IMarineDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<SpeciesDto>> Handle(
        SearchSpeciesQuery request,
        CancellationToken cancellationToken)
    {
        var query = _context.BahamianSpecies.AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.ToLower();
            // CA1862: This is an EF Core query - ToLower().Contains() is the correct pattern
            // for case-insensitive database searches that translates properly to SQL
#pragma warning disable CA1862
            query = query.Where(s =>
                s.ScientificName.ToLower().Contains(term) ||
                s.CommonName.ToLower().Contains(term) ||
                (s.LocalName != null && s.LocalName.ToLower().Contains(term)));
#pragma warning restore CA1862
        }

        if (request.Category.HasValue)
        {
            query = query.Where(s => s.Category == request.Category.Value);
        }

        if (request.IsInvasive.HasValue)
        {
            query = query.Where(s => s.IsInvasive == request.IsInvasive.Value);
        }

        if (request.IsThreatened.HasValue)
        {
            query = query.Where(s => s.IsThreatened == request.IsThreatened.Value);
        }

        return await query
            .OrderBy(s => s.CommonName)
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
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }
}
