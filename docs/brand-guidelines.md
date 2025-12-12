# Brand & Asset Guidelines

This repository ships carefully crafted assets so the CoralLedger Blue identity looks consistent across the README, social previews, and the web UI. Use the guidance below whenever you adjust logos, colors, or favicons.

## Logos

- **Icon** (`wwwroot/images/logos/logo-icon.svg`): Use this glyph when space is tight (favicons, avatar badges, map legends). The icon is monochrome and works on dark and light surfaces.
- **Wordmark** (`wwwroot/images/logos/logo-wordmark.svg`): Use when you need to spell out “CoralLedger Blue” without the icon. Keep it horizontal and avoid stretching.
- **Full lockup** (`wwwroot/images/logos/logo-full.svg`): Pair this with marketing materials, the README hero, or the login experience. Apply `aria-hidden="true"` when it’s purely decorative.
- **Favicons** (`wwwroot/images/logos/favicons/*`): Generated PNG and ICO files live inside this folder. The favicon uses the icon glyph with the teal gradient, so it remains recognizable on browser tabs and home screens.

## Color palette & typography

- The CSS token source-of-truth lives in `wwwroot/css/base/_variables.css` and is documented in `docs/design-system.md`. Use the same palette in brand assets: neon teal gradients (#2EE3FF → #00C5A1) on dark navy backgrounds (#031226).
- When redrawing assets, keep a high contrast ratio (WCAG 4.5:1 for text) and respect the `prefers-contrast: more` overrides to ensure readability.
- Typography in the marketing assets uses Inter + JetBrains Mono; include hinting and medium weights if you re-export the PNG/OG images to stay consistent with the UI.

## Regenerating assets

1. Run `python scripts/create_brand_assets.py` to regenerate `github-header.png` (1280×320) and `og-image.png` (1200×630). The script draws the gradient, the coral node, and the tagline (“Marine Intelligence for the Bahamas Blue Economy”). Commit both PNGs after regeneration.
2. Run `python scripts/create_favicons.py` whenever the gradient, accent colors, or icon changes. It writes the PNG/ICO files under `wwwroot/images/logos/favicons/`.
3. After regenerating, double-check `App.razor` and `manifest.json` for favicon references—update the `<link rel>` tags if new filenames are introduced.
4. Keep `docs/README` references pointing to `github-header.png` (hero) and `og-image.png` (social preview). Update the README badges if you add new brand-related assets (e.g., an “Accessibility-certified” badge).

## Usage guidance

- **README hero**: The top of `README.md` shows `github-header.png`. Keep the banner dimensions at 1280×320 and match the coral node gradient to the design system tokens.
- **Social previews**: `og-image.png` (1200×630) is the canonical preview for GitHub/Twitter/LinkedIn. Use descriptive alt text when referencing it.
- **Favicons**: Do not scale the `favicon.ico` beyond its generated sizes. Browsers read the embedded sizes from the ICO, so keep `favicon-16x16`, `favicon-32x32`, and `favicon.ico` in sync.
- **App nav headers**: The application should only ever use the full lockup (`logo-full.svg`) inside the nav or login hero. Avoid mixing other colors or glows.
- **File naming**: Keep the current names; if you need a different version (light/dark), place it next to the existing files and update the `<link>` tags in `App.razor` or the README references.

## Alignment with documentation

- Document any palette updates in `docs/design-system.md` so engineers understand which CSS variables changed.
- Mention branded story updates in `docs/brand-guidelines.md` and link to them from `docs/CONTRIBUTING.md` under the “Visual regression & accessibility docs” section.
- When referencing logos in Markdown, include relative paths so GitHub renders them (`![CoralLedger Blue](github-header.png)`).

By following these guidelines, the CoralLedger Blue identity remains cohesive across GitHub, the app shell, and the Aspire deployment stack.
