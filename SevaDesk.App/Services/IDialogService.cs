using Microsoft.UI.Xaml.Controls;

namespace SevaDesk_App.Services;

public interface IDialogService
{
    Task<ContentDialogResult> ShowConfirmationAsync(string title, string content, string primaryButtonText = "Yes", string secondaryButtonText = "No");
    Task<(ContentDialogResult Result, string? Input)> ShowInputAsync(string title, string content, string placeholderText = "", string primaryButtonText = "OK", string secondaryButtonText = "Cancel");
    Task ShowAlertAsync(string title, string content, string closeButtonText = "OK");
    Task<ContentDialogResult> ShowCustomDialogAsync(ContentDialog dialog);
}
