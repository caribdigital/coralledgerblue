# Developer Guide

## Quick Start (30 seconds)

```bash
# Prerequisites: .NET 10 SDK + Docker Desktop
git clone https://github.com/caribdigital/coralledgerblue.git
cd coralledgerblue
dotnet run --project src/CoralLedger.Blue.AppHost
```

The Aspire dashboard opens at `https://localhost:17088` with links to all services.

## Project Structure

```
CoralLedgerBlue/
├── src/
│   ├── CoralLedger.Blue.AppHost/        # .NET Aspire orchestration
│   ├── CoralLedger.Blue.ServiceDefaults/ # Shared service configuration
│   ├── CoralLedger.Blue.Domain/          # Domain entities, enums, value objects
│   ├── CoralLedger.Blue.Application/     # CQRS commands/queries, interfaces
│   ├── CoralLedger.Blue.Infrastructure/  # EF Core, external APIs, services
│   └── CoralLedger.Blue.Web/             # Blazor Server + WASM frontend
├── tests/
│   ├── CoralLedger.Blue.Domain.Tests/        # Domain entity unit tests
│   ├── CoralLedger.Blue.Infrastructure.Tests/ # Service integration tests
│   └── CoralLedger.Blue.E2E.Tests/           # Playwright E2E tests
├── docs/                            # Documentation
└── scripts/                         # Utility scripts
```

## Architecture Overview

**Clean Architecture** with these layers:
- **Domain**: Pure C# entities with factory methods, no dependencies
- **Application**: MediatR CQRS, interfaces, DTOs
- **Infrastructure**: EF Core + PostGIS, external API clients, Semantic Kernel AI
- **Web**: Blazor Server/WASM Auto mode, Leaflet.js maps

## Key Technologies

| Component | Technology |
|-----------|------------|
| Runtime | .NET 10 |
| Database | PostgreSQL 16 + PostGIS 3.4 |
| ORM | EF Core 10 + NetTopologySuite |
| CQRS | MediatR 12.4 |
| Mapping | Leaflet.js 1.9.4 |
| AI | Semantic Kernel + Azure OpenAI |
| Jobs | Quartz.NET 3.13 |
| Testing | xUnit, FluentAssertions, Playwright |

## Configuration

### User Secrets (Development)

```bash
# Initialize user secrets
cd src/CoralLedger.Blue.Web
dotnet user-secrets init

# Set API keys
dotnet user-secrets set "GlobalFishingWatch:ApiKey" "your-gfw-key"
dotnet user-secrets set "ProtectedPlanet:ApiToken" "your-pp-token"
dotnet user-secrets set "MarineAI:ApiKey" "your-azure-openai-key"
```

See `secrets.template.json` for all available settings.

### Environment Variables (Production)

```bash
GlobalFishingWatch__ApiKey=your-key
ProtectedPlanet__ApiToken=your-token
MarineAI__ApiKey=your-key
MarineAI__Endpoint=https://your-endpoint.openai.azure.com/
```

## Running Tests

```bash
# All unit tests
dotnet test --filter "FullyQualifiedName!~E2E"

# E2E tests (requires app running)
dotnet run --project src/CoralLedger.Blue.AppHost &
dotnet test tests/CoralLedger.Blue.E2E.Tests

# Specific test category
dotnet test --filter "Map"
```

### Test Coverage

| Suite | Tests | Coverage |
|-------|-------|----------|
| Domain | 40+ | Domain entities, value objects |
| Infrastructure | 80+ | Services, API clients, spatial |
| E2E | 34+ | Map page, navigation, visual |

## Spatial Development

### Dr. Thorne's 10 GIS Rules

1. **SRID Commandments**: Store in 4326, calculate in 32618 (UTM Zone 18N)
2. **Bahamas Bounding Box**: -80.5 to -72.0 longitude, 20.0 to 28.0 latitude
3. **GIST Indexes**: All spatial columns must be indexed
4. **Simplification**: 4 tiers (full/detail/medium/low) via `?resolution=` param
5. **Area/Distance**: Use geography type for spherical accuracy
6. **Point-in-Polygon**: `&&` operator + `ST_Intersects` pattern
7. **Precision Doctrine**: 5 decimal coords, 1 decimal temps
8. **Temporal-Spatial**: Every observation has `ObservedAt` + latency
9. **Raster Protocol**: Point extraction only, never full GeoTIFF
10. **Validation Gates**: 10-gate `SpatialValidationService`

### Key Spatial Classes

- `BahamasSpatialConstants` - EEZ bounds, SRID constants
- `SpatialValidationService` - All 10 validation gates
- `MpaProximityService` - MPA containment/proximity analysis
- `ReefHealthCalculator` - Spatial health metrics

## External APIs

| API | Purpose | Docs |
|-----|---------|------|
| NOAA Coral Reef Watch | Bleaching data (SST, DHW) | [ERDDAP](https://coastwatch.pfeg.noaa.gov/erddap/) |
| Global Fishing Watch | Vessel tracking | [API v3](https://globalfishingwatch.org/our-apis/) |
| Protected Planet | WDPA boundaries | [API](https://api.protectedplanet.net/) |

## Common Tasks

### Add a New MPA

1. Get WDPA ID from Protected Planet
2. Call `SyncMpaFromWdpaCommand` with the ID
3. Verify in database with `GetMpaByIdQuery`

### Add a New Species

1. Add to `BahamianSpeciesSeeder`
2. Include: scientific name, common name, local name, conservation status
3. Run migrations

### Create an API Endpoint

1. Add handler in `Application/Features/`
2. Register endpoint in `Web/Endpoints/`
3. Add to `api-reference.md`

## Troubleshooting

### Database Connection Issues

```bash
# Check if PostgreSQL is running
docker ps | grep postgres

# Reset Aspire resources
dotnet run --project src/CoralLedger.Blue.AppHost -- --reset
```

### Map Not Loading

1. Check browser console for JS errors
2. Verify Leaflet.js is loaded (`wwwroot/lib/leaflet/`)
3. Check `/api/mpas/geojson` returns valid GeoJSON

### E2E Tests Failing

```bash
# Install Playwright browsers
pwsh tests/CoralLedger.Blue.E2E.Tests/bin/Debug/net10.0/playwright.ps1 install

# Run with headed browser for debugging
dotnet test --filter "MapTests" -- Playwright.LaunchOptions.Headless=false
```

## Roadmap Status

| Phase | Status | Description |
|-------|--------|-------------|
| Phase 1-2 | Complete | Spatial foundation, interactive map |
| Phase 3 | Complete | Hardening, spatial excellence, testing |
| Phase 4 | Complete | Citizen science, PWA |
| Phase 5 | Complete | Natural language query (RAG) |

### Potential Improvements

- [ ] Redis distributed caching (currently MemoryCache)
- [ ] Spanish/Haitian Creole translations
- [ ] PostGIS query optimization
- [ ] Additional E2E test coverage

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines.

## Resources

- [Architecture](architecture.md) - Detailed system design
- [API Reference](api-reference.md) - Endpoint documentation
- [Phase 1 Delivery](phase1-delivery.md) - Foundation details
- [Phase 2 Delivery](phase2-delivery.md) - Map implementation
