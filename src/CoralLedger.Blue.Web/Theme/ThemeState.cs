using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace CoralLedger.Blue.Web.Theme;

public interface IThemeState
{
    ThemeMode CurrentMode { get; }

    event Action<ThemeMode>? ThemeChanged;

    ValueTask InitializeAsync();

    ValueTask ToggleModeAsync();

    ValueTask SetModeAsync(ThemeMode mode);
}

public sealed class ThemeState : IThemeState
{
    private readonly IJSRuntime _jsRuntime;
    private readonly ILogger<ThemeState> _logger;
    private bool _initialized;
    private ThemeMode _currentMode = ThemeMode.Dark;

    public ThemeMode CurrentMode => _currentMode;

    public event Action<ThemeMode>? ThemeChanged;

    public ThemeState(IJSRuntime jsRuntime, ILogger<ThemeState> logger)
    {
        _jsRuntime = jsRuntime;
        _logger = logger;
    }

    public async ValueTask InitializeAsync()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;

        try
        {
            var stored = await _jsRuntime.InvokeAsync<string>("CoralLedgerThemeManager.getMode");
            _currentMode = stored is not null && Enum.TryParse<ThemeMode>(stored, true, out var parsed)
                ? parsed
                : ThemeMode.Dark;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load theme preference from localStorage, defaulting to Dark mode");
            _currentMode = ThemeMode.Dark;
        }

        await ApplyModeAsync(_currentMode);
        ThemeChanged?.Invoke(_currentMode);
    }

    public async ValueTask ToggleModeAsync()
    {
        var next = _currentMode == ThemeMode.Dark ? ThemeMode.Light : ThemeMode.Dark;
        await SetModeAsync(next);
    }

    public async ValueTask SetModeAsync(ThemeMode mode)
    {
        if (_currentMode == mode && _initialized)
        {
            await ApplyModeAsync(mode);
            return;
        }

        _currentMode = mode;
        await ApplyModeAsync(mode);
        ThemeChanged?.Invoke(_currentMode);
    }

    private ValueTask ApplyModeAsync(ThemeMode mode)
    {
        var scriptMode = mode.ToString().ToLowerInvariant();
        try
        {
            return _jsRuntime.InvokeVoidAsync("CoralLedgerThemeManager.setMode", scriptMode);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save theme preference to localStorage");
            return ValueTask.CompletedTask;
        }
    }
}
