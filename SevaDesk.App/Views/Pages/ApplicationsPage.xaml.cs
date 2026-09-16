using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SevaDesk_App.ViewModels.Pages;
using SevaDesk_App.Services;
using SevaDesk.Core.Models;

namespace SevaDesk_App.Views.Pages;

public sealed partial class ApplicationsPage : Page
{
    public ApplicationsViewModel ViewModel { get; } = new();

    public ApplicationsPage()
    {
        InitializeComponent();
    }

    public static string FormatRupee(decimal amount) => $"₹{amount:N0}";
    public static bool HasStatus(string status) => !string.IsNullOrWhiteSpace(status);
    public static Visibility VisibleIf(bool condition) => condition ? Visibility.Visible : Visibility.Collapsed;
    public static Visibility CollapsedIf(bool condition) => condition ? Visibility.Collapsed : Visibility.Visible;

    public static string GetHeaderButtonText(int tabIndex) => tabIndex == 0 ? "New Template" : "New Application";

    private void AppTabSelector_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
    {
        if (sender.SelectedItem == TabCatalog)
        {
            ViewModel.SelectedTabIndex = 0;
        }
        else if (sender.SelectedItem == TabSubmissions)
        {
            ViewModel.SelectedTabIndex = 1;
        }
    }

    private void HeaderAction_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedTabIndex == 0)
        {
            NewTemplate_Click(sender, e);
        }
        else
        {
            NewApplication_Click(sender, e);
        }
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedTabIndex == 0)
        {
            await ViewModel.LoadTemplatesAsync();
        }
        else
        {
            await ViewModel.LoadApplicationsAsync();
        }
    }

    private void NewTemplate_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.CreateNewTemplate();
    }

    private async void SaveTemplate_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.SaveTemplateAsync();
    }

    private async void DeleteTemplate_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedTemplate == null) return;

        var res = await AppServices.Dialogs.ShowConfirmationAsync(
            "Delete Form Template?",
            $"Are you sure you want to remove '{ViewModel.SelectedTemplate.Title}' from the scheme catalog?",
            "Delete",
            "Cancel"
        );

        if (res == ContentDialogResult.Primary)
        {
            await ViewModel.DeleteTemplateAsync();
        }
    }

    private async void TestPortal_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedTemplate != null && !string.IsNullOrWhiteSpace(ViewModel.SelectedTemplate.PortalUrl))
        {
            await ViewModel.OpenPortalUrlCommand.ExecuteAsync(ViewModel.SelectedTemplate.PortalUrl);
        }
    }

    private void TemplateSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is TextBox tb)
        {
            ViewModel.TemplateSearchQuery = tb.Text.Trim();
            _ = ViewModel.LoadTemplatesAsync();
        }
    }

    private void CategoryFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox cb && cb.SelectedItem is ComboBoxItem item)
        {
            ViewModel.CategoryFilter = item.Content?.ToString() ?? "All";
            _ = ViewModel.LoadTemplatesAsync();
        }
    }

    private void LaunchPortal_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedApplication != null)
        {
            _ = ViewModel.OpenPortalUrlCommand.ExecuteAsync(ViewModel.SelectedApplication.PortalName);
        }
    }

    private async void SetDraft_Click(object sender, RoutedEventArgs e) => await ViewModel.UpdateApplicationStatusAsync("Draft");
    private async void SetDocsReady_Click(object sender, RoutedEventArgs e) => await ViewModel.UpdateApplicationStatusAsync("Docs Uploaded");
    private async void SetSubmitted_Click(object sender, RoutedEventArgs e) => await ViewModel.UpdateApplicationStatusAsync("Submitted");
    private async void SetCompleted_Click(object sender, RoutedEventArgs e) => await ViewModel.UpdateApplicationStatusAsync("Completed");

    private async void NewApplication_Click(object sender, RoutedEventArgs e)
    {
        var txtTitle = new TextBox { PlaceholderText = "e.g. SSC CGL 2026, PAN Form 49A, PM Kisan", Margin = new Thickness(0, 4, 0, 8) };
        var txtCustomer = new TextBox { PlaceholderText = "e.g. Ramesh Kumar", Margin = new Thickness(0, 4, 0, 8) };
        var txtPortal = new TextBox { PlaceholderText = "e.g. ssc.gov.in or onlineservices.nsdl.com", Margin = new Thickness(0, 4, 0, 8) };
        var txtAppNo = new TextBox { PlaceholderText = "e.g. REG-2026-001 (or leave blank)", Margin = new Thickness(0, 4, 0, 8) };
        var txtServiceFee = new NumberBox { Value = 100, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline, SmallChange = 10, Margin = new Thickness(0, 4, 0, 8) };
        var txtGovtFee = new NumberBox { Value = 0, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline, SmallChange = 10, Margin = new Thickness(0, 4, 0, 8) };
        var txtDocs = new TextBox { Text = "Photo, Signature, Aadhaar Card, Marksheet", Margin = new Thickness(0, 4, 0, 0) };

        var dialog = new ContentDialog
        {
            Title = "Register New Citizen Application",
            Content = new ScrollViewer
            {
                MaxHeight = 450,
                Content = new StackPanel
                {
                    Spacing = 4,
                    Children =
                    {
                        new TextBlock { Text = "Scheme / Exam Title *", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold },
                        txtTitle,
                        new TextBlock { Text = "Citizen Name *", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold },
                        txtCustomer,
                        new TextBlock { Text = "Portal URL / Name", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold },
                        txtPortal,
                        new TextBlock { Text = "Application / Registration No.", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold },
                        txtAppNo,
                        new TextBlock { Text = "Service Charge (₹)", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold },
                        txtServiceFee,
                        new TextBlock { Text = "Govt Fee (₹)", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold },
                        txtGovtFee,
                        new TextBlock { Text = "Required Documents (comma separated)", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold },
                        txtDocs
                    }
                }
            },
            PrimaryButtonText = "Save Application",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary
        };

        var res = await AppServices.Dialogs.ShowCustomDialogAsync(dialog);
        if (res == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(txtTitle.Text))
        {
            var newApp = new ApplicationItem
            {
                Title = txtTitle.Text.Trim(),
                CustomerName = string.IsNullOrWhiteSpace(txtCustomer.Text) ? "Walk-in Citizen" : txtCustomer.Text.Trim(),
                PortalName = string.IsNullOrWhiteSpace(txtPortal.Text) ? "gov.in" : txtPortal.Text.Trim(),
                ApplicationNumber = string.IsNullOrWhiteSpace(txtAppNo.Text) ? $"APP-{DateTime.Now:yyyyMMdd}-{DateTime.Now.Millisecond}" : txtAppNo.Text.Trim(),
                ServiceCharge = (decimal)(double.IsNaN(txtServiceFee.Value) ? 100 : txtServiceFee.Value),
                GovtFee = (decimal)(double.IsNaN(txtGovtFee.Value) ? 0 : txtGovtFee.Value),
                RequiredDocs = string.IsNullOrWhiteSpace(txtDocs.Text) ? "Aadhaar Card" : txtDocs.Text.Trim(),
                Status = "Draft",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await ViewModel.AddApplicationAsync(newApp);
        }
    }
}
