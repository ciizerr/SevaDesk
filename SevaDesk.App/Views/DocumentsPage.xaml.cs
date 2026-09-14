using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SevaDesk_App.ViewModels;
using SevaDesk.Core.Models;

namespace SevaDesk_App.Views;

public sealed partial class DocumentsPage : Page
{
    public DocumentsViewModel ViewModel { get; } = new();

    public DocumentsPage()
    {
        InitializeComponent();
    }

    public static bool HasStatus(string status) => !string.IsNullOrWhiteSpace(status);

    private void Refresh_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.LoadDocuments();
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

    private void MoveToShared_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.MoveDocumentCommand.Execute("01_Shared Documents");
    }

    private void MoveToApplications_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.MoveDocumentCommand.Execute("02_Applications");
    }

    private void MoveToPrint_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.MoveDocumentCommand.Execute("03_Ready to Print");
    }
}
