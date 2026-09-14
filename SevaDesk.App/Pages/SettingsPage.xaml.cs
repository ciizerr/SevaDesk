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
        Loaded += SettingsPage_Loaded;
    }

    private void SettingsPage_Loaded(object sender, RoutedEventArgs e)
    {
        TabSelector.SelectedItem = TabAppearance;
    }

    public static bool Not(bool v) => !v;

    private async void CheckLanguageUpdates_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.CheckLanguageUpdatesCommand.ExecuteAsync(null);
    }

    private void TabSelector_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
    {
        if (PanelAppearance == null) return;

        var selected = sender.SelectedItem;
        PanelAppearance.Visibility = selected == TabAppearance ? Visibility.Visible : Visibility.Collapsed;
        PanelProfile.Visibility = selected == TabProfile ? Visibility.Visible : Visibility.Collapsed;
        PanelHardware.Visibility = selected == TabHardware ? Visibility.Visible : Visibility.Collapsed;
        PanelStorage.Visibility = selected == TabStorage ? Visibility.Visible : Visibility.Collapsed;
        PanelAbout.Visibility = selected == TabAbout ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.OpenWorkingFolderCommand.Execute(null);
    }

    private async void BrowseWorkingFolder_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.BrowseWorkingFolderCommand.ExecuteAsync(null);
    }

    private async void AddCustomWatchFolder_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.AddCustomWatchFolderCommand.ExecuteAsync(null);
    }

    private void RemoveCustomWatchFolder_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string path)
        {
            ViewModel.RemoveCustomWatchFolder(path);
        }
    }

    private void Backup_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.BackupDatabaseCommand.Execute(null);
    }

    private void OpenGitHub_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.OpenGitHubCommand.Execute(null);
    }

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.ResetDefaultsCommand.Execute(null);
    }

    private void ReportBug_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.ReportBugCommand.Execute(null);
    }

    private void RequestFeature_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.RequestFeatureCommand.Execute(null);
    }

    private void Community_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.OpenCommunityCommand.Execute(null);
    }

    private void BuyMeCoffee_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.OpenBuyMeCoffeeCommand.Execute(null);
    }

    private void CopyUpi_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.CopyUpiCommand.Execute(null);
    }
}
