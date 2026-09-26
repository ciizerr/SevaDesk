using Microsoft.UI.Xaml.Controls;
using SevaDesk.Core.Models;

namespace SevaDesk_App.Services;

public enum DeleteSessionFileAction
{
    Cancel,
    KeepOnDesktop,
    BackupAndDelete
}

public enum DeleteCustomerAction
{
    Cancel,
    DeleteAndKeepBackup,
    DeleteAndWipeAll
}

public interface IDialogService
{
    Task<ContentDialogResult> ShowConfirmationAsync(string title, string content, string primaryButtonText = "Yes", string secondaryButtonText = "No");
    Task<(ContentDialogResult Result, string? Input)> ShowInputAsync(string title, string content, string placeholderText = "", string primaryButtonText = "OK", string secondaryButtonText = "Cancel");
    Task ShowAlertAsync(string title, string content, string closeButtonText = "OK");
    Task<ContentDialogResult> ShowCustomDialogAsync(ContentDialog dialog);
    Task<(DeleteSessionFileAction Action, int FileCount)> PromptDeleteActiveSessionAsync(ActiveSessionItem item, Microsoft.UI.Xaml.XamlRoot? xamlRoot = null);
    Task<DeleteCustomerAction> PromptDeleteCustomerAsync(Customer customer, Microsoft.UI.Xaml.XamlRoot? xamlRoot = null);
}
