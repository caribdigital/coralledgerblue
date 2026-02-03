namespace CoralLedger.Blue.Web.Services;

/// <summary>
/// Service for showing toast notifications to users
/// </summary>
public interface IToastService
{
    /// <summary>
    /// Shows a success toast notification
    /// </summary>
    void ShowSuccess(string message, string? title = null, int autoCloseDuration = 5000);

    /// <summary>
    /// Shows an error toast notification
    /// </summary>
    void ShowError(string message, string? title = null, int autoCloseDuration = 5000);

    /// <summary>
    /// Shows a warning toast notification
    /// </summary>
    void ShowWarning(string message, string? title = null, int autoCloseDuration = 5000);

    /// <summary>
    /// Shows an info toast notification
    /// </summary>
    void ShowInfo(string message, string? title = null, int autoCloseDuration = 5000);

    /// <summary>
    /// Event raised when a new toast is shown
    /// </summary>
    event EventHandler<ToastEventArgs>? OnToastShown;
}

/// <summary>
/// Event arguments for toast notifications
/// </summary>
public class ToastEventArgs : EventArgs
{
    public Guid Id { get; init; }
    public string Message { get; init; } = "";
    public string? Title { get; init; }
    public ToastType Type { get; init; }
    public int AutoCloseDuration { get; init; }
}

/// <summary>
/// Types of toast notifications
/// </summary>
public enum ToastType
{
    Success,
    Error,
    Warning,
    Info
}
