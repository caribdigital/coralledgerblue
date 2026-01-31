# Localization Guide

This document explains how to use and extend the localization (i18n) feature in CoralLedger Blue.

## Supported Languages

The application currently supports three languages:
- **English (en)** - Default language
- **Spanish (es)** - For Spanish-speaking users in the Caribbean
- **Haitian Creole (ht)** - For Haitian-speaking communities in the Bahamas

## User Experience

### Changing Language

Users can change the language using the language selector dropdown in the header navigation bar. The selector displays:
- 🇬🇧 English
- 🇪🇸 Español
- 🇭🇹 Kreyòl

When a user selects a new language:
1. The preference is saved in a cookie (`.AspNetCore.Culture`)
2. The page reloads to apply the new culture
3. All localized text updates to the selected language

### Language Persistence

The language preference persists for **1 year** via a browser cookie. Users don't need to reselect their language on subsequent visits.

## Developer Guide

### Adding Localized Text to Components

1. **Inject the localizer** in your Razor component:
   ```razor
   @inject IStringLocalizer<SharedResources> Localizer
   ```

2. **Use localized strings** in your markup:
   ```razor
   <h1>@Localizer["Dashboard_Title"]</h1>
   <button>@Localizer["Common_Save"]</button>
   ```

### Adding New Translation Keys

To add new translatable strings:

1. **Open all three resource files**:
   - `SharedResources.resx` (English - base language)
   - `SharedResources.es.resx` (Spanish)
   - `SharedResources.ht.resx` (Haitian Creole)

2. **Add the same key to all files** with translations:

   **SharedResources.resx** (English):
   ```xml
   <data name="Dashboard_NewFeature" xml:space="preserve">
     <value>New Feature</value>
   </data>
   ```

   **SharedResources.es.resx** (Spanish):
   ```xml
   <data name="Dashboard_NewFeature" xml:space="preserve">
     <value>Nueva Función</value>
   </data>
   ```

   **SharedResources.ht.resx** (Haitian Creole):
   ```xml
   <data name="Dashboard_NewFeature" xml:space="preserve">
     <value>Nouvo Karakteristik</value>
   </data>
   ```

### Naming Conventions

Resource keys follow a hierarchical naming pattern:
- `Nav_*` - Navigation items (Nav_Dashboard, Nav_Map)
- `Dashboard_*` - Dashboard-specific text
- `Map_*` - Map-related text
- `Common_*` - Common UI elements (Common_Save, Common_Cancel)
- `Obs_*` - Observation form text
- `Alert_*` - Alert/notification text

### File Locations

- **Resource Files**: `src/CoralLedger.Blue.Web/Resources/`
- **Language Selector Component**: `src/CoralLedger.Blue.Web/Components/Shared/LanguageSelector.razor`
- **JavaScript Helper**: `src/CoralLedger.Blue.Web/wwwroot/js/localization.js`
- **Configuration**: `src/CoralLedger.Blue.Web/Program.cs` (lines 29-51)

## Testing Localization

### Manual Testing

1. Run the application
2. Use the language selector to switch between languages
3. Navigate to different pages to verify translations
4. Check that the preference persists after page reload

### Testing URL Parameters

You can also test language changes via URL query string:
```
https://localhost:5001/?culture=es
https://localhost:5001/dashboard?culture=ht
```

## Architecture

### How It Works

1. **ASP.NET Core Localization Middleware** (`UseRequestLocalization()`) determines the current culture
2. **Culture Providers** check (in order):
   - Query string parameter (`?culture=es`)
   - Cookie (`.AspNetCore.Culture`)
   - Accept-Language HTTP header
3. **Resource Files** (`.resx`) contain the translated strings
4. **IStringLocalizer** provides access to localized strings in components
5. **JavaScript Helper** (`localization.js`) safely manages culture cookies

### Security Considerations

- Culture codes are **validated** against a whitelist (`en`, `es`, `ht`)
- **No eval()** is used - all JavaScript uses safe helper functions
- Cookie uses **SameSite=Lax** to prevent CSRF attacks
- Cookie has **1-year expiration** (not session-only)

## Extending to More Languages

To add support for a new language (e.g., French):

1. Add the culture to `Program.cs`:
   ```csharp
   new CultureInfo("fr"),    // French
   ```

2. Create a new resource file:
   ```
   SharedResources.fr.resx
   ```

3. Copy all keys from `SharedResources.resx` and translate values to French

4. Update `localization.js` to include the new culture:
   ```javascript
   const validCultures = ['en', 'es', 'ht', 'fr'];
   ```

5. Update the `LanguageSelector` component:
   ```csharp
   new CultureOption { Code = "fr", DisplayName = "Français" }
   ```

## Translation Guidelines

When translating:
- Keep **technical terms** in English where appropriate (MPA, DHW, km²)
- Maintain **formatting placeholders** (`{0}`, `{1}`)
- Preserve **HTML tags** if present
- Keep **button text short** for UI space constraints
- Use **formal language** for Caribbean Spanish and Haitian Creole
- Test on **mobile devices** to ensure text fits in UI elements

## Resources

- [ASP.NET Core Globalization and Localization](https://docs.microsoft.com/en-us/aspnet/core/fundamentals/localization)
- [Resource Files in .NET](https://docs.microsoft.com/en-us/dotnet/core/extensions/create-resource-files)
- [Culture and Language Codes](https://docs.microsoft.com/en-us/openspecs/windows_protocols/ms-lcid/a9eac961-e77d-41a6-90a5-ce1a8b0cdb9c)
