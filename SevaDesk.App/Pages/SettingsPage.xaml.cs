using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SevaDesk_App.ViewModels;

namespace SevaDesk_App.Pages;

public sealed partial class SettingsPage : Page
{
    public SettingsViewModel ViewModel { get; } = new();

    public SettingsPage()
    {
        InitializeComponent();
    }

    public static bool HasStatus(string status) => !string.IsNullOrWhiteSpace(status);

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.OpenWorkingFolderCommand.Execute(null);
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.SaveSettingsCommand.Execute(null);
    }

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.ResetDefaultsCommand.Execute(null);
    }
}
