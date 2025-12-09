# Contributing to CoralLedger Blue

Thank you for your interest in contributing to CoralLedger Blue! This project aims to support marine conservation in The Bahamas through open-source technology.

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/)
- Git
- IDE (Visual Studio 2022, VS Code, or JetBrains Rider)

### Setup

1. Fork the repository
2. Clone your fork:
   ```bash
   git clone https://github.com/YOUR-USERNAME/coralledger-blue.git
   cd coralledger-blue
   ```
3. Run the application:
   ```bash
   dotnet run --project src/CoralLedger.AppHost
   ```

## How to Contribute

### Reporting Bugs

- Check existing issues first
- Use the bug report template
- Include steps to reproduce
- Provide environment details (.NET version, OS, Docker version)

### Suggesting Features

- Open a discussion first for large features
- Describe the use case and benefit
- Consider the project roadmap alignment

### Pull Requests

1. Create a feature branch:
   ```bash
   git checkout -b feature/your-feature-name
   ```

2. Make your changes following our conventions

3. Write/update tests as needed

4. Ensure the build passes:
   ```bash
   dotnet build
   dotnet test
   ```

5. Submit a PR with a clear description

## Code Conventions

### C# Style

- Follow Microsoft C# coding conventions
- Use meaningful names
- Keep methods small and focused
- Prefer immutability where practical

### Architecture Rules

- **Domain Layer**: No external dependencies
- **Application Layer**: Only depends on Domain
- **Infrastructure**: Implements Application interfaces
- **Web**: Uses MediatR for all data operations

### Commit Messages

Use conventional commits:
```
feat: add vessel tracking endpoint
fix: correct MPA boundary calculation
docs: update architecture diagram
refactor: extract spatial query helpers
```

### Branch Naming

- `feature/` - New features
- `fix/` - Bug fixes
- `docs/` - Documentation updates
- `refactor/` - Code refactoring

## Good First Issues

Look for issues labeled `good-first-issue`:

- Add new MPA icons
- Improve mobile styling
- Add unit tests for entities
- Expand seed data
- Documentation improvements

## Project Structure

```
src/
├── CoralLedger.Domain/        # Business entities (no deps)
├── CoralLedger.Application/   # Use cases, CQRS
├── CoralLedger.Infrastructure/ # Data access, external services
├── CoralLedger.Web/           # Blazor UI
├── CoralLedger.AppHost/       # Aspire orchestration
└── CoralLedger.ServiceDefaults/
```

## Testing

### Running Tests
```bash
dotnet test
```

### Test Structure
- Unit tests: `tests/CoralLedger.Domain.Tests/`
- Integration tests: `tests/CoralLedger.Infrastructure.Tests/`
- E2E tests: `tests/CoralLedger.Web.Tests/`

### Writing Tests
- Use xUnit
- Use FluentAssertions for assertions
- Use Moq for mocking
- Test one thing per test

## Documentation

- Update README.md for user-facing changes
- Update docs/ for architectural changes
- Add XML comments for public APIs
- Keep code self-documenting where possible

### Visual regression & accessibility docs

- Start the Aspire stack with `Scripts/coralledgerblue/Start-CoralLedgerBlueAspire.ps1 -Detached` before running `dotnet test tests/CoralLedger.E2E.Tests/CoralLedger.E2E.Tests.csproj`. The Playwright fixture now probes HTTP/HTTPS and stores failure screenshots under `tests/CoralLedger.E2E.Tests/playwright-artifacts/`.
- Set `CoralReefWatch__UseMockData=true` when running the host for visual regression so the Coral Reef Watch client uses the local mock dataset (`mock-bleaching-data.json`) instead of hitting NOAA.
- Record Lighthouse/axe output in `docs/accessibility-audit.md` every time the UI changes so regressions are apparent and priorities are traceable.
- Keep `docs/brand-guidelines.md` updated whenever logos, favicons, `github-header.png`, or `og-image.png` change; re-run `python scripts/create_brand_assets.py` / `python scripts/create_favicons.py` and commit the regenerated assets.

## Code of Conduct

- Be respectful and inclusive
- Focus on constructive feedback
- Help others learn and grow
- Keep discussions on-topic

## Questions?

- Open a GitHub Discussion
- Check existing documentation
- Review closed issues/PRs

## License

By contributing, you agree that your contributions will be licensed under the MIT License.

---

Thank you for helping protect the Bahamas' marine ecosystems through code!
