using CommunityToolkit.Mvvm.ComponentModel;
using SevaDesk_App.Services;

namespace SevaDesk_App.ViewModels;

/// <summary>
/// Base ViewModel for all pages that need to show toast notifications.
/// Provides convenience methods that route through the global ToastService.
/// </summary>
public partial class StatusViewModel : ObservableObject
{
    public void ShowStatus(string message, ToastSeverity severity = ToastSeverity.Success)
    {
        ToastService.Instance.Show(message, severity);
    }

    public void ShowSuccess(string message) => ToastService.Instance.ShowSuccess(message);
    public void ShowError(string message) => ToastService.Instance.ShowError(message);
    public void ShowWarning(string message) => ToastService.Instance.ShowWarning(message);
    public void ShowInfo(string message) => ToastService.Instance.ShowInfo(message);
}
