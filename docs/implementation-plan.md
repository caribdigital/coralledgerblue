# CoralLedger Blue Implementation Plan

This is a focused execution checklist that builds on the UI & map plan in C:/Projects/Tasks/coralledgerblue/CoralLedger-Blue-UI-Map-Implementation-Plan.md. It captures the immediate Phase 1 items we can own while keeping the large roadmap handy for later sprints.

## Phase 1 Foundation (done but needs artifacts)

1. **Design system & tokens**
   - Central profile lives in src/CoralLedger.Web/wwwroot/css/base/_variables.css, CoralLedgerTheme, and ThemeState, which push the variables into MudBlazor controls.
   - Theme toggle markup is in src/CoralLedger.Web/Components/Shared/ThemeToggle.razor.
   - Ensure docs/design-system.md mirrors any future token changes.

2. **Brand assets & tooling**
   - Vector and raster logos sit under src/CoralLedger.Web/wwwroot/images/logos/.
   - scripts/create_brand_assets.py and scripts/create_favicons.py regenerate the README/og header plus favicons; re-run when the palette or tagline changes.
   - Manifest and favicon tags were updated in src/CoralLedger.Web/Components/App.razor and wwwroot/manifest.json.

3. **Playwright fixture**
   - tests/CoralLedger.E2E.Tests/PlaywrightFixture.cs now builds HTTPS-ignoring contexts and loads tests/CoralLedger.E2E.Tests/appsettings.e2e.json.
   - Keep the map/base URLs, timeouts, and visual assertions in sync with the deployed UI.

## Phase 1 Action Items

1. **Finalize accessibility audit notes**
   - Run Lighthouse/axe (locally and via Playwright) across / and /map.
   - Record findings, severity, and remediation steps in docs/accessibility-audit.md so the remediation backlog is auditable.
   - Flag blocking issues (keyboard traps, focus, contrast) for priority spikes before Phase 2.

2. **Document logo usage / persona guidance**
   - Author docs/brand-guidelines.md describing the icon, wordmark, full lockup, and how the scripts generate them.
   - Explain placement expectations (navbars, favicons, README hero) and acceptable color combinations (solid vs. gradient) to minimize ad-hoc styling.

3. **Polish README & CONTRIBUTING**
   - Keep the hero banner and badges near the top of README.md, referencing github-header.png and og-image.png.
   - Add a 'Visual regression' and 'Accessibility' subsection that link to docs/accessibility-audit.md and the Protected Planet/token setup in docs/DEVELOPER.md.
   - Update docs/CONTRIBUTING.md with the current phase, the build/test cadence (`dotnet build`, `dotnet test`, `dotnet test tests/CoralLedger.E2E.Tests/CoralLedger.E2E.Tests.csproj`), and a refreshed set of 'good first issue' ideas derived from the roadmap.

4. **Stabilize visual/E2E regression**
   - Double-check E2E_BASE_URL (overridden in tests/CoralLedger.E2E.Tests/appsettings.e2e.json or via an environment variable) so Playwright points at the Aspire endpoint (default https://localhost:7232).
   - Capture baseline screenshots inside tests/CoralLedger.E2E.Tests/Tests/MapTests.cs and compare them with the README hero/og visuals for regression tracking.
   - Document the required start/stop commands (Scripts/coralledgerblue/Start-CoralLedgerBlueAspire.ps1 and Scripts/coralledgerblue/Stop-CoralLedgerBlueAspire.ps1) so contributors can rerun the suite.

5. **Lock in next-phase map deliverables**
   - Write docs/map-improvements.md summarizing the Map Experience stories from the plan file (dark map tiles, MPA styling, control panel, alerts).
   - Inventory the needed components (MapLegend, MapControlPanel, LoadingSkeleton, EmptyState, ErrorState) so Phase 2 work can be scoped accurately.

## Dependencies / Risks

- The app depends on Postgres/PostGIS, Redis, and outbound calls to NOAA/Global Fishing Watch/Protected Planet. The Protected Planet API key is configured via ProtectedPlanetOptions in src/CoralLedger.Infrastructure/ExternalServices/ProtectedPlanetClient.cs; set it with `dotnet user-secrets set "ProtectedPlanet:ApiToken" "your-token"` or the secrets template.
- Visual/E2E runs rely on Playwright with an HTTPS context - our fixture ignores invalid certs, but the host still needs to respond quickly. If remote APIs time out, mock or cache those calls so the suite avoids ERR_TIMED_OUT failures.
- Capture every relevant script or asset change in these docs so contributors know how to restart Aspire, rerun scripts/create_*, and keep the roadmap aligned.
