using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SevaDesk_App.ViewModels.Pages;

namespace SevaDesk_App.Views.Pages;

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

    // Static helpers for x:Bind function calls
    public static bool Not(bool v) => !v;
    public static Visibility BoolToVisibility(bool v) => v ? Visibility.Visible : Visibility.Collapsed;

    private void TabSelector_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
    {
        if (PanelAppearance == null) return;

        var selected = sender.SelectedItem;
        PanelAppearance.Visibility = selected == TabAppearance ? Visibility.Visible : Visibility.Collapsed;
        PanelProfile.Visibility    = selected == TabProfile    ? Visibility.Visible : Visibility.Collapsed;
        PanelHardware.Visibility   = selected == TabHardware   ? Visibility.Visible : Visibility.Collapsed;
        PanelStorage.Visibility    = selected == TabStorage    ? Visibility.Visible : Visibility.Collapsed;
    }

    private void SaveChanges_Click(object sender, RoutedEventArgs e)
        => ViewModel.SaveAllChangesCommand.Execute(null);

    private async void CheckLanguageUpdates_Click(object sender, RoutedEventArgs e)
        => await ViewModel.CheckLanguageUpdatesCommand.ExecuteAsync(null);

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
        => ViewModel.OpenWorkingFolderCommand.Execute(null);

    private async void BrowseWorkingFolder_Click(object sender, RoutedEventArgs e)
        => await ViewModel.BrowseWorkingFolderCommand.ExecuteAsync(null);

    private async void AddCustomWatchFolder_Click(object sender, RoutedEventArgs e)
        => await ViewModel.AddCustomWatchFolderCommand.ExecuteAsync(null);

    private void RemoveCustomWatchFolder_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string path)
        {
            ViewModel.RemoveCustomWatchFolder(path);
        }
    }

    private void Backup_Click(object sender, RoutedEventArgs e)
        => ViewModel.BackupDatabaseCommand.Execute(null);
}
