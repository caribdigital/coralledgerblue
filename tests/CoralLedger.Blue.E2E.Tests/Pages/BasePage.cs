namespace CoralLedger.Blue.E2E.Tests.Pages;

/// <summary>
/// Base page object providing common functionality for all pages
/// </summary>
public abstract class BasePage
{
    protected IPage Page { get; }
    protected string BaseUrl { get; }

    public abstract string Path { get; }

    protected BasePage(IPage page, string baseUrl)
    {
        Page = page;
        BaseUrl = baseUrl;
    }

    public async Task NavigateAsync()
    {
        await Page.GotoAsync($"{BaseUrl}{Path}");
        await WaitForPageLoadAsync();
    }

    protected virtual async Task WaitForPageLoadAsync()
    {
        // Wait for Blazor to initialize
        await Page.WaitForFunctionAsync("window.Blazor !== undefined");
        // Wait for DOM to be ready (NetworkIdle times out with SignalR connections)
        await Page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
    }

    public async Task<bool> HasTitleAsync(string expectedTitle)
    {
        var title = await Page.TitleAsync();
        return title.Contains(expectedTitle);
    }

    public async Task<ILocator> GetNavigationAsync()
    {
        return Page.Locator("nav");
    }
}
