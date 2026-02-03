namespace CoralLedger.Blue.Web.Services;

/// <summary>
/// Default implementation of IToastService that uses event-based notifications
/// </summary>
public class ToastService : IToastService
{
    public event EventHandler<ToastEventArgs>? OnToastShown;

    public void ShowSuccess(string message, string? title = null, int autoCloseDuration = 5000)
    {
        ShowToast(message, title, ToastType.Success, autoCloseDuration);
    }

    public void ShowError(string message, string? title = null, int autoCloseDuration = 5000)
    {
        ShowToast(message, title, ToastType.Error, autoCloseDuration);
    }

    public void ShowWarning(string message, string? title = null, int autoCloseDuration = 5000)
    {
        ShowToast(message, title, ToastType.Warning, autoCloseDuration);
    }

    public void ShowInfo(string message, string? title = null, int autoCloseDuration = 5000)
    {
        ShowToast(message, title, ToastType.Info, autoCloseDuration);
    }

    private void ShowToast(string message, string? title, ToastType type, int autoCloseDuration)
    {
        var args = new ToastEventArgs
        {
            Id = Guid.NewGuid(),
            Message = message,
            Title = title,
            Type = type,
            AutoCloseDuration = autoCloseDuration
        };

        OnToastShown?.Invoke(this, args);
    }
}
