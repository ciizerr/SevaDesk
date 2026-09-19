using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SevaDesk_App.ViewModels.Pages;

namespace SevaDesk_App.Views.Pages;

public sealed partial class AboutPage : Page
{
    public AboutViewModel ViewModel { get; } = new();

    public AboutPage()
    {
        InitializeComponent();
    }

    private void OpenGitHub_Click(object sender, RoutedEventArgs e)
        => ViewModel.OpenGitHubCommand.Execute(null);

    private void ReportBug_Click(object sender, RoutedEventArgs e)
        => ViewModel.ReportBugCommand.Execute(null);

    private void RequestFeature_Click(object sender, RoutedEventArgs e)
        => ViewModel.RequestFeatureCommand.Execute(null);

    private void Community_Click(object sender, RoutedEventArgs e)
        => ViewModel.OpenCommunityCommand.Execute(null);

    private void BuyMeCoffee_Click(object sender, RoutedEventArgs e)
        => ViewModel.OpenBuyMeCoffeeCommand.Execute(null);

    private void CopyUpi_Click(object sender, RoutedEventArgs e)
        => ViewModel.CopyUpiCommand.Execute(null);

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        // Reset defaults is still in SettingsViewModel — delegate via a fresh instance for now
        var settingsVm = new SettingsViewModel();
        settingsVm.ResetDefaultsCommand.Execute(null);
    }

    private async void ResetAppData_Click(object sender, RoutedEventArgs e)
        => await ViewModel.ResetAppDataCommand.ExecuteAsync(null);
}
