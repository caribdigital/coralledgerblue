# CoralLedger Blue - Architecture Documentation

## Overview

CoralLedger Blue is built using **Clean Architecture** principles, implemented as a **Modular Monolith**. This architecture provides clear separation of concerns while maintaining the simplicity of a single deployable unit.

## Architectural Principles

### Clean Architecture Layers

```
┌─────────────────────────────────────────────────────────────┐
│                    Presentation Layer                        │
│                  (CoralLedger.Web - Blazor)                 │
├─────────────────────────────────────────────────────────────┤
│                   Infrastructure Layer                       │
│      (CoralLedger.Infrastructure - EF Core, PostGIS)        │
├─────────────────────────────────────────────────────────────┤
│                    Application Layer                         │
│        (CoralLedger.Application - MediatR, CQRS)            │
├─────────────────────────────────────────────────────────────┤
│                      Domain Layer                            │
│     (CoralLedger.Domain - Entities, Value Objects)          │
└─────────────────────────────────────────────────────────────┘
```

### Dependency Flow

Dependencies flow **inward** - outer layers depend on inner layers, never the reverse:

- **Domain** → No dependencies (pure business logic)
- **Application** → Depends on Domain
- **Infrastructure** → Depends on Application and Domain
- **Web** → Depends on Application and Infrastructure

## Project Structure

```
CoralLedger-Blue/
├── src/
│   ├── CoralLedger.Domain/
│   │   ├── Common/
│   │   │   ├── BaseEntity.cs
│   │   │   ├── IAggregateRoot.cs
│   │   │   └── IAuditableEntity.cs
│   │   ├── Entities/
│   │   │   ├── MarineProtectedArea.cs
│   │   │   └── Reef.cs
│   │   └── Enums/
│   │       ├── IslandGroup.cs
│   │       ├── MpaStatus.cs
│   │       ├── ProtectionLevel.cs
│   │       └── ReefHealth.cs
│   │
│   ├── CoralLedger.Application/
│   │   ├── Common/
│   │   │   └── Interfaces/
│   │   │       ├── IMarineDbContext.cs
│   │   │       └── IDateTimeService.cs
│   │   ├── Features/
│   │   │   └── MarineProtectedAreas/
│   │   │       ├── DTOs/
│   │   │       │   ├── MpaSummaryDto.cs
│   │   │       │   └── MpaDetailDto.cs
│   │   │       └── Queries/
│   │   │           ├── GetAllMpas/
│   │   │           └── GetMpaById/
│   │   └── DependencyInjection.cs
│   │
│   ├── CoralLedger.Infrastructure/
│   │   ├── Data/
│   │   │   ├── MarineDbContext.cs
│   │   │   ├── Configurations/
│   │   │   │   ├── MarineProtectedAreaConfiguration.cs
│   │   │   │   └── ReefConfiguration.cs
│   │   │   └── Seeding/
│   │   │       └── BahamasMpaSeeder.cs
│   │   ├── Services/
│   │   │   └── DateTimeService.cs
│   │   └── DependencyInjection.cs
│   │
│   ├── CoralLedger.Web/
│   │   ├── Components/
│   │   │   ├── Layout/
│   │   │   │   ├── MainLayout.razor
│   │   │   │   └── NavMenu.razor
│   │   │   └── Pages/
│   │   │       ├── Home.razor
│   │   │       ├── Map.razor
│   │   │       └── MpaInfoPanel.razor
│   │   └── Program.cs
│   │
│   ├── CoralLedger.AppHost/
│   │   └── AppHost.cs
│   │
│   └── CoralLedger.ServiceDefaults/
│       └── Extensions.cs
│
├── docs/
├── Directory.Build.props
├── Directory.Packages.props
└── CoralLedger.sln
```

## Layer Details

### Domain Layer (`CoralLedger.Domain`)

The innermost layer containing enterprise business logic and entities.

**Responsibilities:**
- Entity definitions with business rules
- Value objects for complex types
- Domain enumerations
- Aggregate roots

**Key Components:**

```csharp
// MarineProtectedArea - Aggregate Root
public class MarineProtectedArea : BaseEntity, IAggregateRoot, IAuditableEntity
{
    public string Name { get; private set; }
    public Geometry Boundary { get; private set; }      // PostGIS geometry
    public Point Centroid { get; private set; }         // Calculated center
    public ProtectionLevel ProtectionLevel { get; private set; }
    public IslandGroup IslandGroup { get; private set; }
    // ... factory methods and business logic
}
```

**Design Decisions:**
- Private setters enforce encapsulation
- Factory methods (`Create()`) ensure valid entity creation
- No dependencies on external frameworks

### Application Layer (`CoralLedger.Application`)

Contains application-specific business logic and orchestration.

**Responsibilities:**
- CQRS queries and commands (MediatR)
- DTOs for data transfer
- Interface definitions for infrastructure
- Validation rules (FluentValidation)

**CQRS Pattern:**

```csharp
// Query
public record GetAllMpasQuery : IRequest<IReadOnlyList<MpaSummaryDto>>;

// Handler
public class GetAllMpasQueryHandler : IRequestHandler<GetAllMpasQuery, IReadOnlyList<MpaSummaryDto>>
{
    private readonly IMarineDbContext _context;

    public async Task<IReadOnlyList<MpaSummaryDto>> Handle(GetAllMpasQuery request, CancellationToken ct)
    {
        return await _context.MarineProtectedAreas
            .AsNoTracking()
            .Select(mpa => new MpaSummaryDto { /* mapping */ })
            .ToListAsync(ct);
    }
}
```

### Infrastructure Layer (`CoralLedger.Infrastructure`)

Implements interfaces defined in Application layer.

**Responsibilities:**
- Entity Framework Core DbContext
- PostGIS spatial configuration
- Database seeding
- External service implementations

**PostGIS Configuration:**

```csharp
public class MarineDbContext : DbContext, IMarineDbContext
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("postgis");
        // Entity configurations with spatial indexes
    }
}
```

**Spatial Index Configuration:**

```csharp
builder.Property(e => e.Boundary)
    .HasColumnType("geometry(Geometry, 4326)")
    .IsRequired();

builder.HasIndex(e => e.Boundary)
    .HasMethod("GIST");  // PostGIS spatial index
```

### Presentation Layer (`CoralLedger.Web`)

Blazor Server application for user interaction.

**Responsibilities:**
- Razor components and pages
- User interface layout
- MediatR query dispatching
- Real-time updates (SignalR)

**Component Pattern:**

```razor
@page "/map"
@inject IMediator Mediator
@rendermode InteractiveServer

@code {
    private IReadOnlyList<MpaSummaryDto>? _mpas;

    protected override async Task OnInitializedAsync()
    {
        _mpas = await Mediator.Send(new GetAllMpasQuery());
    }
}
```

## Aspire Orchestration

.NET Aspire manages the distributed application lifecycle.

**AppHost Configuration:**

```csharp
var builder = DistributedApplication.CreateBuilder(args);

// PostgreSQL with PostGIS
var postgres = builder.AddPostgres("postgres")
    .WithImage("postgis/postgis")
    .WithImageTag("16-3.4")
    .WithDataVolume("coralledger-postgres-data")
    .WithPgAdmin();

var marineDb = postgres.AddDatabase("marinedb");

// Web application
builder.AddProject<Projects.CoralLedger_Web>("web")
    .WithReference(marineDb)
    .WaitFor(marineDb)
    .WithExternalHttpEndpoints();
```

**Benefits:**
- Automatic container orchestration
- Service discovery
- Health checks
- Centralized dashboard
- Local development parity with production

## Data Flow

### Request Flow

```
User Request
    │
    ▼
┌─────────────────┐
│   Blazor Page   │
│   (Map.razor)   │
└────────┬────────┘
         │ MediatR.Send()
         ▼
┌─────────────────┐
│  Query Handler  │
│ (GetAllMpas)    │
└────────┬────────┘
         │ IMarineDbContext
         ▼
┌─────────────────┐
│  MarineDbContext│
│   (EF Core)     │
└────────┬────────┘
         │ SQL + PostGIS
         ▼
┌─────────────────┐
│   PostgreSQL    │
│   + PostGIS     │
└─────────────────┘
```

## Spatial Data Handling

### Coordinate Reference System

All spatial data uses **SRID 4326** (WGS84) - the standard GPS coordinate system.

### Geometry Types

| Entity | Geometry Type | Usage |
|--------|---------------|-------|
| MPA Boundary | Polygon/MultiPolygon | Protected area boundaries |
| MPA Centroid | Point | Map markers, center calculations |
| Reef Location | Geometry | Points, lines, or polygons |

### Spatial Queries

```csharp
// Find MPAs containing a point
var mpas = await _context.MarineProtectedAreas
    .Where(m => m.Boundary.Contains(point))
    .ToListAsync();

// Find MPAs within distance
var nearby = await _context.MarineProtectedAreas
    .Where(m => m.Centroid.IsWithinDistance(point, distanceMeters))
    .ToListAsync();
```

## Configuration

### Central Package Management

`Directory.Packages.props` centralizes NuGet package versions:

```xml
<PackageVersion Include="NetTopologySuite" Version="2.5.0" />
<PackageVersion Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="10.0.0" />
<PackageVersion Include="MediatR" Version="12.4.1" />
```

### Common Build Properties

`Directory.Build.props` sets defaults for all projects:

```xml
<PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
</PropertyGroup>
```

## Testing Strategy

### Recommended Test Structure

```
tests/
├── CoralLedger.Domain.Tests/
│   └── Entities/
│       └── MarineProtectedAreaTests.cs
├── CoralLedger.Application.Tests/
│   └── Features/
│       └── MarineProtectedAreas/
│           └── GetAllMpasQueryTests.cs
└── CoralLedger.Infrastructure.Tests/
    └── Data/
        └── MarineDbContextTests.cs
```

### Testing Approaches

| Layer | Test Type | Tools |
|-------|-----------|-------|
| Domain | Unit Tests | xUnit, FluentAssertions |
| Application | Unit Tests | xUnit, Moq, FluentAssertions |
| Infrastructure | Integration Tests | TestContainers, PostgreSQL |
| Web | E2E Tests | Playwright, bUnit |

## Security Considerations

- Connection strings stored in Aspire secrets
- HTTPS enforced in production
- Database credentials auto-generated by Aspire
- No sensitive data in source control

## Performance Optimizations

- **GIST indexes** on spatial columns for fast queries
- **AsNoTracking()** for read-only queries
- **Projection** with Select() to limit data transfer
- **Connection pooling** via Npgsql
