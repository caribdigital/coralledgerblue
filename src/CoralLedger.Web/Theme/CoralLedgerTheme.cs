using System.Collections.Generic;

namespace CoralLedger.Web.Theme;

public sealed class CoralLedgerTheme
{
    private static readonly IReadOnlyDictionary<string, string> CssVariables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["--color-primary"] = "#2EE3FF",
        ["--color-primary-dark"] = "#1BA8C3",
        ["--color-secondary"] = "#00C5A1",
        ["--color-success"] = "#5DE285",
        ["--color-warning"] = "#F7C549",
        ["--color-error"] = "#FB7A6A",
        ["--color-surface"] = "#050C1A",
        ["--color-surface-alt"] = "#0C1B33",
        ["--color-border"] = "rgba(255, 255, 255, 0.18)",
        ["--color-text"] = "#F5FBFF",
        ["--color-text-muted"] = "rgba(245, 251, 255, 0.70)",
        ["--focus-ring"] = "#58A6FF",
        ["--focus-ring-offset"] = "4px",
        ["--shadow-elevated"] = "0 25px 60px rgba(3, 7, 20, 0.65)",
        ["--shadow-soft"] = "0 14px 40px rgba(6, 10, 30, 0.55)",
        ["--pill-radius"] = "999px",
        ["--radius-base"] = "14px",
        ["--transition-base"] = "0.35s ease",
        ["--content-max-width"] = "1280px"
    };

    public static CoralLedgerTheme Dark { get; } = new();

    private CoralLedgerTheme()
    {
    }

    public IReadOnlyDictionary<string, string> Variables => CssVariables;

    public string Primary => CssVariables["--color-primary"];

    public string Secondary => CssVariables["--color-secondary"];

    public string Surface => CssVariables["--color-surface"];

    public string SurfaceAlt => CssVariables["--color-surface-alt"];

    public string Text => CssVariables["--color-text"];

    public string TextMuted => CssVariables["--color-text-muted"];

    public string FocusRing => CssVariables["--focus-ring"];

    public string Border => CssVariables["--color-border"];
}
