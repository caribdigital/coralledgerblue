namespace CoralLedger.Blue.E2E.Tests.Pages;

/// <summary>
/// Page object for the Observations page
/// </summary>
public class ObservationsPage : BasePage
{
    public override string Path => "/observations";

    public ObservationsPage(IPage page, string baseUrl) : base(page, baseUrl)
    {
    }

    public async Task<bool> HasObservationFormAsync()
    {
        // Use First to avoid strict mode violations when multiple elements match
        var form = Page.Locator("form, [class*='observation-form']").First;
        return await form.IsVisibleAsync();
    }

    public async Task<bool> HasObservationListAsync()
    {
        // First, click the list view button if available
        var listViewButton = Page.Locator("button:has(.bi-list), button:has-text('list')").First;
        try
        {
            if (await listViewButton.IsVisibleAsync())
            {
                await listViewButton.ClickAsync();
                await Page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
            }
        }
        catch { /* ignore click errors */ }

        // Look for observation list container (may be empty state or actual items)
        var list = Page.Locator(".observation-list, [class*='observation'], .list-group").First;
        return await list.IsVisibleAsync();
    }

    public async Task<ILocator> GetSpeciesDropdownAsync()
    {
        return Page.Locator("select[name*='species'], [class*='species-dropdown'], [aria-label*='Species']");
    }

    public async Task<ILocator> GetLocationInputAsync()
    {
        return Page.Locator("input[name*='location'], [class*='location-input'], [aria-label*='Location']");
    }

    public async Task<ILocator> GetSubmitButtonAsync()
    {
        return Page.Locator("button[type='submit'], [class*='submit-btn'], button:has-text('Submit')");
    }

    public async Task<ILocator> GetPhotoUploadAsync()
    {
        return Page.Locator("input[type='file'], [class*='photo-upload']");
    }

    public async Task FillObservationFormAsync(
        string speciesName = "Unknown",
        string location = "Nassau, Bahamas",
        string notes = "Test observation")
    {
        // Try to fill species if dropdown exists
        var speciesDropdown = await GetSpeciesDropdownAsync();
        if (await speciesDropdown.IsVisibleAsync())
        {
            await speciesDropdown.SelectOptionAsync(new SelectOptionValue { Label = speciesName });
        }

        // Try to fill location if input exists
        var locationInput = await GetLocationInputAsync();
        if (await locationInput.IsVisibleAsync())
        {
            await locationInput.FillAsync(location);
        }

        // Look for notes/description field
        var notesInput = Page.Locator("textarea, input[name*='note'], input[name*='description']");
        if (await notesInput.IsVisibleAsync())
        {
            await notesInput.FillAsync(notes);
        }
    }
}
