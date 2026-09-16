namespace SevaDesk_App.Services;

public enum ToastSeverity
{
    Success,
    Error,
    Warning,
    Info
}

/// <summary>
/// Centralized toast notification service — singleton accessible from any ViewModel.
/// Fires ToastRequested events consumed by the ToastNotificationControl in MainWindow.
/// </summary>
public sealed class ToastService
{
    private static ToastService? _instance;
    public static ToastService Instance => _instance ??= new ToastService();

    public event Action<string, ToastSeverity, int>? ToastRequested;

    private CancellationTokenSource? _dismissCts;

    private ToastService() { }

    /// <summary>
    /// Show a toast notification with the given message, severity, and auto-dismiss duration.
    /// </summary>
    public void Show(string message, ToastSeverity severity = ToastSeverity.Success, int durationMs = 3000)
    {
        // Cancel any pending dismiss from a previous toast
        _dismissCts?.Cancel();
        _dismissCts?.Dispose();
        _dismissCts = new CancellationTokenSource();

        MainWindow.Instance?.DispatcherQueue.TryEnqueue(() =>
        {
            ToastRequested?.Invoke(message, severity, durationMs);
        });
    }

    public void ShowSuccess(string message) => Show(message, ToastSeverity.Success, 3000);
    public void ShowError(string message) => Show(message, ToastSeverity.Error, 5000);
    public void ShowWarning(string message) => Show(message, ToastSeverity.Warning, 4000);
    public void ShowInfo(string message) => Show(message, ToastSeverity.Info, 3000);
}
