# Map Experience Improvements (Phase 2)

This document summarizes the Phase 2 work that extends `C:\Projects\Tasks\coralledgerblue\CoralLedger-Blue-UI-Map-Implementation-Plan.md` into actionable components and data contracts in the repository. It focuses on the map, the controls, and the supporting UX/UX artifacts that the Playwright suite and future automated audits rely on.

## Goals

- Deliver a cohesive dark-map canvas (matching the design tokens in `wwwroot/css/base/_variables.css`) with well-styled Marine Protected Areas (MPAs), bleaching alerts, and vessel overlays.
- Provide clear, accessible controls and legends so each map layer can be toggled, described, and consumed via keyboard/screen readers.
- Reuse the `MapLegend`, `MapControlPanel`, loading skeletons, and empty/error states already under `src/CoralLedger.Web.Client/Components/` to create predictable fallbacks during data fetches.

## Core deliverables

| Deliverable | Notes |
|-------------|-------|
| Dark map base | Use CartoDB Dark Matter tiles (fallback to Stamen Toner) so the map mirrors the overall dark theme described in `docs/design-system.md`. Include attribution text and graceful loading states. |
| MPA styling | Color-code MPAs by protection level (No-Take=red, Highly Protected=amber, Lightly Protected=teal). Add tooltip/highlight on hover and glow on selection to comply with the WCAG non-color guidance by pairing color with pattern or legend label. |
| Map control panel | The `MapControlPanel` component should expose layer toggles (MPAs, vessels, alerts, reefs), a date range filter, locate-me, zoom, and fullscreen actions. All controls must announce `aria-pressed` and read their state to screen readers. |
| Legend & status | `MapLegend` uses `MapLegendItem` data to mirror the pastel gradients in `_variables.css`. It should explain color/pattern combinations so color is not the only differentiator. |
| Loading/Error states | Reuse `LoadingSkeleton`, `EmptyState`, and `ErrorState` components to keep data fetching deterministic (e.g., map data, bleaching alerts). Document which states correspond to which API call so visual regression tests can snap consistent placeholders. |

## Component inventory

| Component | Responsibility | Notes |
|-----------|----------------|-------|
| `MapLegend` / `MapLegendItem` | Describes the color/pattern mapping for MPAs, alerts, and vessel statuses. | Pair legend text with icons so color is not the sole cue. |
| `MapControlPanel` | Hosts layer toggles, date filters, locate/fullscreen actions. | Should emit events that other components (e.g., `MpaMapComponent`) can consume to update the data layer. Document keyboard shortcuts. |
| `MpaMapComponent` | Initializes the Leaflet map, wires up the `MapControlPanel`, and renders layer data. | Use the global theme for overlays (e.g., `--color-secondary` for active outlines). |
| `LoadingSkeleton`, `EmptyState`, `ErrorState` | Provide consistent UX during data loads, empty responses, or API failures. | Capture default text in tests so visual snapshots stay stable. |
| `ConnectionStatus`, `OfflineIndicator` | Live status inside the layout so users know when the map may show stale data. | Tie these to the `MapControlPanel` so toggles disable themselves when offline. |

## Implementation notes

- Cache the bleaching/vessel data in IndexedDB when possible, but always show the cached time stamp in the legend/control panel so the user knows how fresh the data is.
- Document the required scripts in `docs/CONTRIBUTING.md` (they already reference `Scripts/coralledgerblue/Start-CoralLedgerBlueAspire.ps1`) so contributors can rebuild the app and rerun Playwright. The map components must pass the same theme tokens so the screenshot baselines remain consistent.
- Reference `docs/implementation-plan.md` for Phase 2 execution details and link back to this map summary when updating issues or stories in GitHub.

## Next steps

1. Finalize the Playwright tests that cover each panel state (map loads, layer toggles, empty/alert states) and store baseline screenshots near `tests/CoralLedger.E2E.Tests/playwright-artifacts/`.
2. Update `docs/accessibility-audit.md` with any new WCAG findings discovered while polishing the map (focus traps, color contrast).
3. Iterate on the `MapControlPanel` interactions after E2E baseline verification so Phase 3 can focus on offline/autonomous features.
