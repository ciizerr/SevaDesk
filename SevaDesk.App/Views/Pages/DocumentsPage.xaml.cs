using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SevaDesk_App.ViewModels.Pages;
using SevaDesk.Core.Models;
using SevaDesk_App.Services;

namespace SevaDesk_App.Views.Pages;

public sealed partial class DocumentsPage : Page
{
    public DocumentsViewModel ViewModel { get; } = new();

    public DocumentsPage()
    {
        InitializeComponent();
    }

    public static bool HasStatus(string status) => !string.IsNullOrWhiteSpace(status);
    public static Visibility VisibleIf(bool condition) => condition ? Visibility.Visible : Visibility.Collapsed;
    public static Visibility CollapsedIf(bool condition) => condition ? Visibility.Collapsed : Visibility.Visible;

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.LoadDocumentsAsync();
    }

    private void OpenExplorer_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.OpenInExplorerCommand.Execute(null);
    }

    private void PresetButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is CompressionPreset preset)
        {
            ViewModel.ApplyPresetCommand.Execute(preset);
        }
    }

    private void OpenFile_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.OpenFile();
    }

    private async void MoveToSubfolder_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string subfolderName && !string.IsNullOrWhiteSpace(subfolderName))
        {
            await ViewModel.MoveDocumentToSubfolderAsync(subfolderName);
        }
    }

    private async void NewSubfolder_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedDocument == null) return;

        var customerRoot = ViewModel.GetCustomerRootFolder(ViewModel.SelectedDocument);
        if (string.IsNullOrWhiteSpace(customerRoot))
        {
            ViewModel.ShowError("Cannot determine customer working directory.");
            return;
        }

        var (result, rawName) = await AppServices.Dialogs.ShowInputAsync(
            "Create Customer Subfolder",
            $"Target: {ViewModel.SelectedDocument.CustomerName}",
            "e.g. SSC CGL, PAN Card, Scholarship",
            "Create & Move File",
            "Create Only"
        );
        rawName = rawName?.Trim();
        if (string.IsNullOrWhiteSpace(rawName)) return;

        try
        {
            var appPath = AppServices.FolderManager.EnsureApplicationSubfolder(customerRoot, rawName);
            var subfolderName = System.IO.Path.GetFileName(appPath);

            if (result == ContentDialogResult.Primary)
            {
                await ViewModel.MoveDocumentToSubfolderAsync(subfolderName);
            }
            else if (result == ContentDialogResult.Secondary)
            {
                ViewModel.RefreshCustomerSubfolders();
                ViewModel.ShowSuccess($"Subfolder '{subfolderName}' created.");
            }
        }
        catch (Exception ex)
        {
            ViewModel.ShowError($"Error creating subfolder: {ex.Message}");
        }
    }

    private async void MoveToShared_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.MoveDocumentToSubfolderAsync("Shared Docs");
    }

    private async void MoveToApplications_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.MoveDocumentToSubfolderAsync("Applications");
    }

    private async void MoveToPrint_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.MoveDocumentToSubfolderAsync("Ready to Print");
    }
}
