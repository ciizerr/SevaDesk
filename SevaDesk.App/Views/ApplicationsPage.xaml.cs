using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SevaDesk_App.ViewModels;

namespace SevaDesk_App.Views;

public sealed partial class ApplicationsPage : Page
{
    public ApplicationsViewModel ViewModel { get; } = new();

    public ApplicationsPage()
    {
        InitializeComponent();
    }

    public static string FormatRupee(decimal amount) => $"₹{amount:N0}";
    public static bool HasStatus(string status) => !string.IsNullOrWhiteSpace(status);

    private void Refresh_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.LoadApplications();
    }

    private void LaunchPortal_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedApplication != null)
        {
            _ = ViewModel.OpenPortalUrlCommand.ExecuteAsync(ViewModel.SelectedApplication.PortalName);
        }
    }

    private void SetDraft_Click(object sender, RoutedEventArgs e) => ViewModel.UpdateApplicationStatusCommand.Execute("Draft");
    private void SetDocsReady_Click(object sender, RoutedEventArgs e) => ViewModel.UpdateApplicationStatusCommand.Execute("Docs Uploaded");
    private void SetSubmitted_Click(object sender, RoutedEventArgs e) => ViewModel.UpdateApplicationStatusCommand.Execute("Submitted");
    private void SetCompleted_Click(object sender, RoutedEventArgs e) => ViewModel.UpdateApplicationStatusCommand.Execute("Completed");
}
