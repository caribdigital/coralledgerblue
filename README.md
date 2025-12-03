# CoralLedger Blue

**Open Source Marine Intelligence for the Blue Economy**

[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/)
[![Blazor](https://img.shields.io/badge/Blazor-Server-512BD4)](https://blazor.net/)
[![PostGIS](https://img.shields.io/badge/PostGIS-3.4-336791)](https://postgis.net/)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

CoralLedger Blue is an open-source marine intelligence platform designed to support marine conservation efforts in The Bahamas. Built with cutting-edge .NET 10 technology, it provides tools for monitoring Marine Protected Areas (MPAs), tracking reef health, and enabling citizen science contributions.

## Features

- **Marine Protected Areas Management** - Track and visualize all Bahamas MPAs with spatial data
- **PostGIS Spatial Database** - Full GIS capabilities for marine boundary management
- **Clean Architecture** - Modular monolith design for maintainability and scalability
- **.NET Aspire Orchestration** - One-command deployment with automatic container management
- **Interactive Dashboard** - Blazor-powered web interface for MPA exploration

## Quick Start

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/)

### Run the Application

```bash
# Clone the repository
git clone https://github.com/coralledger/coralledger-blue.git
cd coralledger-blue

# Run with .NET Aspire (starts PostgreSQL/PostGIS automatically)
dotnet run --project src/CoralLedger.AppHost
```

The Aspire dashboard will open at `https://localhost:17088` with links to:
- **Web Application** - Blazor frontend for exploring MPAs
- **pgAdmin** - Database management interface
- **PostgreSQL** - PostGIS-enabled database

## Architecture

CoralLedger Blue follows **Clean Architecture** principles with a modular monolith structure:

```
src/
├── CoralLedger.Domain/          # Entities, Value Objects, Enums
├── CoralLedger.Application/     # CQRS (MediatR), DTOs, Interfaces
├── CoralLedger.Infrastructure/  # EF Core, PostGIS, Data Seeding
├── CoralLedger.Web/             # Blazor Server Application
├── CoralLedger.AppHost/         # .NET Aspire Orchestrator
└── CoralLedger.ServiceDefaults/ # Shared Aspire Configuration
```

### Technology Stack

| Layer | Technology |
|-------|------------|
| **Runtime** | .NET 10 |
| **Frontend** | Blazor Server |
| **Database** | PostgreSQL 16 + PostGIS 3.4 |
| **ORM** | Entity Framework Core 10 |
| **Spatial** | NetTopologySuite |
| **CQRS** | MediatR |
| **Orchestration** | .NET Aspire |
| **Containers** | Docker |

## Database Schema

### Marine Protected Areas
- Spatial boundaries (PostGIS Geometry)
- Protection levels (NoTake, HighlyProtected, LightlyProtected)
- Island group classification
- WDPA (World Database on Protected Areas) integration

### Reefs
- Location geometry
- Health status tracking
- Coral cover and bleaching metrics
- Survey history

## Seeded Data

The application comes pre-seeded with 8 Bahamas Marine Protected Areas:

| MPA | Island Group | Protection Level | Area (km²) |
|-----|--------------|------------------|------------|
| Exuma Cays Land and Sea Park | Exumas | No-Take | 456.0 |
| Andros West Side National Park | Andros | Highly Protected | 1,607.5 |
| Inagua National Park | Inagua | Highly Protected | 743.0 |
| Pelican Cays Land and Sea Park | Abaco | No-Take | 21.0 |
| Lucayan National Park | Grand Bahama | Lightly Protected | 16.0 |
| Conception Island National Park | Long Island | No-Take | 8.5 |
| Black Sound Cay National Reserve | Abaco | Highly Protected | 1.2 |
| Peterson Cay National Park | Grand Bahama | No-Take | 0.6 |

## Project Roadmap

### Phase 1: Spatial Foundation (Current)
- [x] Clean Architecture setup
- [x] PostGIS spatial database
- [x] MPA entity with spatial boundaries
- [x] Bahamas MPA seed data
- [x] Blazor web interface
- [x] .NET Aspire orchestration

### Phase 2: Data Ingestion
- [ ] Global Fishing Watch API integration
- [ ] NOAA Coral Reef Watch data
- [ ] Vessel traffic monitoring
- [ ] Automated data pipelines (Quartz.NET)

### Phase 3: Citizen Science
- [ ] PWA offline support
- [ ] Photo upload with AI species identification
- [ ] Invasive species reporting (Lionfish tracking)
- [ ] Community observations

### Phase 4: AI Intelligence
- [ ] Semantic Kernel integration
- [ ] Natural language queries ("Show reefs with high bleaching risk")
- [ ] Text-to-SQL for spatial queries
- [ ] Predictive analytics

## Contributing

We welcome contributions! Please see [CONTRIBUTING.md](docs/CONTRIBUTING.md) for guidelines.

### Good First Issues
- Add icons for different MPA types
- Improve mobile responsiveness
- Add French/Spanish translations
- Expand MPA seed data

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Acknowledgments

- **Bahamas National Trust** - MPA data and conservation efforts
- **Protected Planet / WDPA** - Global protected areas database
- **Allen Coral Atlas** - Coral reef mapping data
- **.NET Team** - Aspire, Blazor, and EF Core

---

**Built with love for the Bahamas Blue Economy**
