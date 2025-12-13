# CoralLedger Blue - Phase 1 Delivery Document

## Project Overview

**Project:** CoralLedger Blue
**Phase:** 1 - Spatial Foundation
**Delivery Date:** December 2024
**Status:** Complete

## Objective

Build the foundational architecture for an open-source marine intelligence platform targeting the Bahamas Blue Economy, with PostGIS spatial database support and .NET Aspire orchestration.

## Delivered Components

### 1. Solution Architecture

| Component | Description | Status |
|-----------|-------------|--------|
| Clean Architecture | 6-project modular monolith | ✅ Complete |
| Central Package Management | Directory.Packages.props | ✅ Complete |
| Common Build Properties | Directory.Build.props | ✅ Complete |
| Git Repository | Initialized with .gitignore | ✅ Complete |

### 2. Domain Layer (`CoralLedger.Blue.Domain`)

**Entities:**
- `MarineProtectedArea` - Aggregate root with spatial boundaries
- `Reef` - Coral reef locations and health data

**Enumerations:**
- `MpaStatus` - Proposed, Designated, Active, Suspended, Decommissioned
- `ProtectionLevel` - NoTake, HighlyProtected, LightlyProtected, MinimalProtection
- `IslandGroup` - All 15 Bahamas island groups
- `ReefHealth` - Excellent, Good, Fair, Poor, Critical, Unknown

**Base Classes:**
- `BaseEntity` - Common entity properties (Id)
- `IAggregateRoot` - Marker interface for aggregates
- `IAuditableEntity` - Audit trail properties (CreatedAt, ModifiedAt, etc.)

### 3. Application Layer (`CoralLedger.Blue.Application`)

**Interfaces:**
- `IMarineDbContext` - Database context abstraction
- `IDateTimeService` - Time abstraction for testing

**CQRS Queries:**
- `GetAllMpasQuery` - Retrieve all MPAs with summary data
- `GetMpaByIdQuery` - Retrieve detailed MPA with GeoJSON boundary

**DTOs:**
- `MpaSummaryDto` - Lightweight MPA representation for lists
- `MpaDetailDto` - Full MPA details including GeoJSON

**Dependency Injection:**
- MediatR configuration
- FluentValidation registration

### 4. Infrastructure Layer (`CoralLedger.Blue.Infrastructure`)

**Database:**
- `MarineDbContext` - EF Core DbContext with PostGIS extensions
- PostGIS extension enabled (`postgis`)
- SRID 4326 (WGS84) for all spatial data

**Entity Configurations:**
- `MarineProtectedAreaConfiguration` - Spatial columns, GIST indexes
- `ReefConfiguration` - Spatial columns, indexes

**Seeding:**
- `BahamasMpaSeeder` - Pre-populates 8 Bahamas MPAs

**Services:**
- `DateTimeService` - IDateTimeService implementation

### 5. Web Layer (`CoralLedger.Blue.Web`)

**Pages:**
- `Home.razor` - Landing page with feature overview
- `Map.razor` - MPA listing with interactive selection
- `MpaInfoPanel.razor` - Detailed MPA information panel

**Layout:**
- `MainLayout.razor` - Application shell
- `NavMenu.razor` - Navigation sidebar

**Configuration:**
- Blazor Server with Interactive mode
- MediatR integration
- Database initialization and seeding on startup

### 6. Aspire Orchestration (`CoralLedger.Blue.AppHost`)

**Configured Resources:**
- PostgreSQL with PostGIS image (`postgis/postgis:16-3.4`)
- Persistent data volume (`coralledger-postgres-data`)
- pgAdmin for database management
- Web application with external endpoints

## Seed Data - Bahamas Marine Protected Areas

| # | Name | Island Group | Protection | Area (km²) | Established |
|---|------|--------------|------------|------------|-------------|
| 1 | Exuma Cays Land and Sea Park | Exumas | No-Take | 456.0 | 1958 |
| 2 | Andros West Side National Park | Andros | Highly Protected | 1,607.5 | 2002 |
| 3 | Inagua National Park | Inagua | Highly Protected | 743.0 | 1965 |
| 4 | Pelican Cays Land and Sea Park | Abaco | No-Take | 21.0 | 1972 |
| 5 | Lucayan National Park | Grand Bahama | Lightly Protected | 16.0 | 1982 |
| 6 | Conception Island National Park | Long Island | No-Take | 8.5 | 1971 |
| 7 | Black Sound Cay National Reserve | Abaco | Highly Protected | 1.2 | 1988 |
| 8 | Peterson Cay National Park | Grand Bahama | No-Take | 0.6 | 1968 |

## Technology Stack

| Category | Technology | Version |
|----------|------------|---------|
| Runtime | .NET | 10.0 |
| Frontend | Blazor Server | 10.0 |
| Database | PostgreSQL | 16 |
| Spatial | PostGIS | 3.4 |
| ORM | Entity Framework Core | 10.0 |
| Spatial Library | NetTopologySuite | 2.5.0 |
| CQRS | MediatR | 12.4.1 |
| Validation | FluentValidation | 11.11.0 |
| Orchestration | .NET Aspire | 13.0.1 |
| Containers | Docker | Latest |

## NuGet Packages

### Domain
- `NetTopologySuite` 2.5.0
- `NetTopologySuite.IO.GeoJSON` 4.0.0

### Application
- `MediatR` 12.4.1
- `FluentValidation` 11.11.0
- `FluentValidation.DependencyInjectionExtensions` 11.11.0
- `Microsoft.EntityFrameworkCore` 10.0.0

### Infrastructure
- `Aspire.Npgsql.EntityFrameworkCore.PostgreSQL` 9.1.0
- `Npgsql.EntityFrameworkCore.PostgreSQL` 10.0.0
- `Npgsql.EntityFrameworkCore.PostgreSQL.NetTopologySuite` 10.0.0

### Web
- `Aspire.Npgsql.EntityFrameworkCore.PostgreSQL` 9.1.0
- `MediatR` 12.4.1
- `Mapsui.Blazor` 5.0.0 (included for future use)
- `Mapsui.Nts` 5.0.0 (included for future use)

### AppHost
- `Aspire.Hosting.PostgreSQL` 9.1.0

## File Inventory

### Solution Root
```
CoralLedger.sln
Directory.Build.props
Directory.Packages.props
.gitignore
README.md
LICENSE (to be added)
```

### Source Files (27 files)

**Domain (9 files):**
- `Common/BaseEntity.cs`
- `Common/IAggregateRoot.cs`
- `Common/IAuditableEntity.cs`
- `Entities/MarineProtectedArea.cs`
- `Entities/Reef.cs`
- `Enums/IslandGroup.cs`
- `Enums/MpaStatus.cs`
- `Enums/ProtectionLevel.cs`
- `Enums/ReefHealth.cs`

**Application (7 files):**
- `Common/Interfaces/IMarineDbContext.cs`
- `Common/Interfaces/IDateTimeService.cs`
- `Features/MarineProtectedAreas/DTOs/MpaSummaryDto.cs`
- `Features/MarineProtectedAreas/DTOs/MpaDetailDto.cs`
- `Features/MarineProtectedAreas/Queries/GetAllMpas/GetAllMpasQuery.cs`
- `Features/MarineProtectedAreas/Queries/GetMpaById/GetMpaByIdQuery.cs`
- `DependencyInjection.cs`

**Infrastructure (6 files):**
- `Data/MarineDbContext.cs`
- `Data/Configurations/MarineProtectedAreaConfiguration.cs`
- `Data/Configurations/ReefConfiguration.cs`
- `Data/Seeding/BahamasMpaSeeder.cs`
- `Services/DateTimeService.cs`
- `DependencyInjection.cs`

**Web (5 files):**
- `Program.cs`
- `Components/Pages/Home.razor`
- `Components/Pages/Map.razor`
- `Components/Pages/MpaInfoPanel.razor`
- `Components/Layout/NavMenu.razor`

**AppHost (1 file):**
- `AppHost.cs`

## Running the Application

### Prerequisites
1. .NET 10 SDK
2. Docker Desktop (running)

### Start Command
```bash
cd C:\Projects\CoralLedger-Blue
dotnet run --project src/CoralLedger.Blue.AppHost
```

### Access Points
| Service | URL |
|---------|-----|
| Aspire Dashboard | https://localhost:17088 |
| Web Application | (shown in dashboard) |
| pgAdmin | (shown in dashboard) |
| PostgreSQL | localhost:(dynamic port) |

## Known Limitations (Phase 1)

1. **Map Visualization** - Mapsui packages included but interactive map not yet implemented
2. **No Authentication** - Open access, no user management
3. **Approximate Boundaries** - MPA boundaries are circular approximations, not actual shapefiles
4. **No Data Ingestion** - Static seed data only

## Phase 2 Recommendations

1. **Interactive Map** - Implement Mapsui component with actual MPA polygon rendering
2. **Real Boundary Data** - Import actual MPA boundaries from Protected Planet API
3. **Vessel Tracking** - Global Fishing Watch API integration
4. **Background Jobs** - Quartz.NET for scheduled data ingestion
5. **Reef Monitoring** - NOAA Coral Reef Watch data integration

## Acceptance Criteria - Phase 1

| Requirement | Status |
|-------------|--------|
| .NET 10 Aspire solution structure | ✅ Met |
| Clean Architecture implementation | ✅ Met |
| PostGIS spatial database | ✅ Met |
| MPA entity with spatial boundaries | ✅ Met |
| Bahamas MPA seed data | ✅ Met |
| Blazor web interface | ✅ Met |
| One-command startup | ✅ Met |
| Docker container orchestration | ✅ Met |

## Sign-Off

Phase 1 delivers a fully functional foundation for CoralLedger Blue with:
- Production-ready architecture
- Spatial database capabilities
- Extensible design for future features
- Complete documentation

Ready for Phase 2 development.
