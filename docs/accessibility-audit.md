# Accessibility Audit & Visual Regression Checklist

This file captures the current accessibility posture so that remediation work stays auditable while the UI and map evolve. The checklist below mirrors the commitments laid out in `docs/implementation-plan.md` and the lighthouse/axe goals from the UI & map plan.

## Goals

- Run automated audits (Lighthouse, axe-core) against `/` and `/map` and capture the HTML/JSON exports so regressions are traceable.
- Validate keyboard navigation, focus states, and screen reader announcements during every UI refresh.
- Use the Playwright E2E suite as a regression gate because it now takes screenshots on failure and probes both HTTPS and HTTP before navigating.
- Document every blocker/critical issue directly in this file with a prioritized remediation plan.

## Running the audit

1. Start Aspire with `Scripts/coralledgerblue/Start-CoralLedgerBlueAspire.ps1 -Detached` (the host now listens on the same ports that `tests/CoralLedger.E2E.Tests/appsettings.e2e.json` probes: `https://localhost:7232` and `http://localhost:7232`).
2. Set `CoralReefWatch__UseMockData=true` when the web app starts so the Coral Reef Watch client reads `ExternalServices/data/mock-bleaching-data.json` instead of waiting on NOAA.
3. Run `dotnet test tests/CoralLedger.E2E.Tests/CoralLedger.E2E.Tests.csproj`. The fixture will:
   - Probe HTTP/HTTPS endpoints until a responsive URL is found and log the chosen base URL.
   - Capture console errors and failure screenshots under `tests/CoralLedger.E2E.Tests/playwright-artifacts/`.
   - Respect `Headless`/`SlowMo` settings from `appsettings.e2e.json`.
4. Record Lighthouse or axe reports (HTML/JSON) in a new folder such as `docs/logs/accessibility` and link them in this doc so the severity history is preserved.
5. Mention any blocking design tokens (colors, spacing, focus) in `docs/design-system.md` so designers and engineers stay aligned.

## Finding tracker

Use the table below to log issues discovered during each audit run. Link to screenshots, Lighthouse/axe exports, and remediation owners to keep the backlog transparent.

| ID | Severity | Page / Component | Description | Status / Owner | Evidence |
|----|----------|------------------|-------------|----------------|----------|
| A11Y-001 | Critical | MainLayout | Skip link is hidden without styles | Open / @ui-team | Link to screenshot |
| A11Y-002 | Major | Map controls | Toggles do not expose aria-pressed | **Fixed** | MapControlPanel.razor |
| A11Y-003 | Major | Map controls | No keyboard shortcuts for layer toggles | **Fixed** | M/F/A/L/R/? keys |
| A11Y-004 | Major | Map legend | Color is only differentiator | **Fixed** | WCAG patterns + icons |
| A11Y-005 | Minor | Map controls | No offline state indication | **Fixed** | IsOffline parameter |
| A11Y-006 | Minor | Map | No hover tooltips on MPA polygons | **Fixed** | leaflet-map.js hover info |

Replace the placeholder entries above with real findings. Aim to close Major/Critical issues before moving to Phase 2 map work.

## Recent Accessibility Improvements (December 2025)

### MapControlPanel Enhancements
- Added `role="region"` with `aria-label="Map controls"` for screen readers
- Added `fieldset` and `legend` for proper grouping of layer toggles
- Added `aria-describedby` for each toggle checkbox
- Added `accesskey` attributes for direct keyboard access (M, F, A, L, R)
- Added keyboard shortcuts panel (press `?` to show)
- Added `aria-pressed` state for fullscreen toggle
- Added offline warning banner with `role="alert"`
- Added visible focus indicators (`:focus-visible` outlines)

### Map Legend WCAG Compliance
- Added pattern overlays (diagonal stripes, dots, waves) alongside colors
- Added icons (emojis) as secondary visual indicators
- Added `role="list"` and `role="listitem"` for screen reader navigation
- Added `tabindex="0"` for keyboard focus
- Added size differentiation for fishing activity recency dots
- Added pulse animations with reduced motion fallback

### Hover Information
- Added MPA hover info box with detailed information
- Includes name, island group, protection level, and area
- Uses ARIA `role="status"` with `aria-live="polite"`

### Internationalization
- Added localization infrastructure with resource files
- Supported languages: English (default), Spanish, Haitian Creole
- Culture detection via query string, cookie, or Accept-Language header

## Severity guidance

- **Critical:** Breaks functionality for keyboard/screen reader users (keyboard trap, missing labels, blocked form submission).
- **Major:** Prevents reliable interpretation (low contrast, missing landmarks, insufficient focus).
- **Minor:** Cosmetic visual glitches, redundant reporting, or improvements that do not block completion.

Highlight blocked stories in the “Status / Owner” column and include remediation links into existing GitHub issues or projects.

## Visual regression artifacts

- Playwright failure screenshots live under `tests/CoralLedger.E2E.Tests/playwright-artifacts/`. Commit the baseline images and keep them updated whenever the UI/brand changes.
- Store key frames from Lighthouse / axe runs alongside this markdown file (e.g., `docs/accessibility-audit/lighthouse-<date>.html`).
- Capture cross-browser snapshots manually when needed and note the viewport/device in this doc.

## Protected Planet token reminder

The Protected Planet API token is sensitive; keep it in user secrets or environment variables (see `docs/DEVELOPER.md` and `secrets.template.json`). Never check `ProtectedPlanet:ApiToken` into this repo while running audits—use a stub dataset or the mock flag mentioned above.
