# Design System & Theme Foundation

This project uses a token-driven design system that keeps the CoralLedger Blue visual identity consistent across the web shell and the Blazor UI.

## Tokens & Theme Modes

- `app.css` imports `css/base/_variables.css`, which defines the dark mode tokens used by default.
- The same file now contains `:root[data-theme='light']` overrides, so when the `<html>` element is marked with `data-theme="light"` the palette, surface colors, and shadows flip to a light-friendly alternative while keeping the same variable names.
- `prefers-contrast: more` media rules further boost contrast when the user requests it.
- Tokens exposed via `CoralLedger.Web.Theme.CoralLedgerTheme` (registered in `Program.cs`) can be referenced in code if a component needs to peek at the semantic colors directly.

## Theme State & Persistence

- A small script in `App.razor` seeds the `window.CoralLedgerThemeManager` helper. It:
  1. Reads `localStorage.coral-theme-mode` (or respects the OS `prefers-color-scheme` fallback).
  2. Applies the resolved mode to `document.documentElement.dataset.theme` before the CSS files load (avoiding flash-of-unstyled content).
  3. Exposes `.getMode()` / `.setMode(mode)` so Blazor can sync future changes.
- `ThemeState` (scoped service) calls into that helper via `IJSRuntime`, persists the selected mode, and notifies subscribers whenever the theme switches.
- `MainLayout` subscribes to those changes, adding a lightweight `<ThemeToggle />` button next to the navigation links and refreshing the page markup whenever the mode flips.

## Accessibility Hooks

- The design system ships with a keyboard-accessible focus ring (`css/base/_focus.css`) that reads from `--focus-ring`/`--focus-ring-offset` and provides a visible skip link.
- Motion-safe rules are already wired in via `prefers-reduced-motion`, and the theme toggle respects that setting as well.
- High contrast users get an extra boost through the `prefers-contrast: more` rules defined above.

## Branding Assets

- Vector logos now live in `wwwroot/images/logos/` so both in-layout headers and marketing materials can pull the icon, wordmark, and full lockup without rasterizing.
- Favicons and touch icons are generated inside `wwwroot/images/logos/favicons/` via `scripts/create_favicons.py`; the updated `<link rel>` tags in `App.razor` point to those assets for browser tabs and Apple devices.
- The repository ships `github-header.png` (1280×320) for the README hero banner and `og-image.png` (1200×630) for social previews; rerun `scripts/create_brand_assets.py` whenever the palette or tagline changes to keep the assets in sync.

## Future Notes

- If new semantic tokens are added to `_variables.css`, keep the values mirrored in `CoralLedgerTheme` if the colors need to be available in C# code (e.g., for chart accents or inline gradients).
- New components that rely on theme state should inject `IThemeState` so they can react to changes instead of reading `dataset` directly.
