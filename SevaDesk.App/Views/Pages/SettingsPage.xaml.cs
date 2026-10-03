using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
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
        TabSelector.SelectedItem = TabProfile;
    }

    protected override void OnNavigatedFrom(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        ViewModel.AutoSaveProfile();
    }

    // Static helpers for x:Bind function calls
    public static bool Not(bool v) => !v;
    public static Visibility BoolToVisibility(bool v) => v ? Visibility.Visible : Visibility.Collapsed;

    private void TabSelector_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
    {
        DismissKeyboardFocus(null);

        if (PanelProfile == null) return;

        var selected = sender.SelectedItem;
        PanelProfile.Visibility    = selected == TabProfile    ? Visibility.Visible : Visibility.Collapsed;
        PanelAppearance.Visibility = selected == TabAppearance ? Visibility.Visible : Visibility.Collapsed;
        PanelStorage.Visibility    = selected == TabStorage    ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Background_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        DismissKeyboardFocus(e.OriginalSource);
    }

    private void DismissKeyboardFocus(object? originalSource)
    {
        // Don't interfere if the user clicked directly into an interactive input
        if (originalSource is TextBox or ComboBox or Button or ToggleSwitch or Slider)
        {
            return;
        }

        // Move keyboard focus away to the page container
        this.Focus(FocusState.Programmatic);

        // Auto-save any pending profile changes
        ViewModel.AutoSaveProfile();
    }

    private void Input_LostFocus(object sender, RoutedEventArgs e)
    {
        ViewModel.AutoSaveProfile();
    }

    private void SldTtlHours_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        // Smooth real-time preview without any database writes or toast spam
        ViewModel.WorkingFolderTtlDisplay = SettingsViewModel.FormatTtlHours((int)Math.Round(e.NewValue));
    }

    private void SldTtlHours_PointerCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        CommitSliderValue();
    }

    private void SldTtlHours_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        CommitSliderValue();
    }

    private void CommitSliderValue()
    {
        if (SldTtlHours == null) return;
        var hours = (int)Math.Round(SldTtlHours.Value);
        ViewModel.CommitWorkingFolderTtl(hours);
    }

    private void PresetHours_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string tagStr && int.TryParse(tagStr, out var hours))
        {
            if (SldTtlHours != null)
            {
                SldTtlHours.Value = hours;
            }
            ViewModel.CommitWorkingFolderTtl(hours);
        }
    }

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

    private async void Backup_Click(object sender, RoutedEventArgs e)
        => await ViewModel.BackupDatabaseCommand.ExecuteAsync(null);

    private async void Restore_Click(object sender, RoutedEventArgs e)
        => await ViewModel.RestoreDatabaseCommand.ExecuteAsync(null);

}
