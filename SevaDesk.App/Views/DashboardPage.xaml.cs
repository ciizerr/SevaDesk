using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SevaDesk_App.Services;
using SevaDesk_App.ViewModels;

namespace SevaDesk_App.Views;

public sealed partial class DashboardPage : Page
{
    public DashboardViewModel ViewModel { get; } = new();

    public DashboardPage()
    {
        InitializeComponent();
        Loaded += async (s, e) => await ViewModel.InitializeAsync();
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.LoadActiveSessionsAsync();
    }

    private async void NewCustomerSession_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new NewCustomerDialog
        {
            XamlRoot = XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            var customer = await AppServices.Customers.CreateAsync(new SevaDesk.Core.Models.Customer
            {
                Name = dialog.CustomerName,
                Mobile = string.IsNullOrWhiteSpace(dialog.Mobile) ? null : dialog.Mobile,
                Village = string.IsNullOrWhiteSpace(dialog.Village) ? null : dialog.Village,
                IdReference = string.IsNullOrWhiteSpace(dialog.IdRef) ? null : dialog.IdRef,
                Notes = string.IsNullOrWhiteSpace(dialog.Notes) ? null : dialog.Notes
            });

            await AppServices.Sessions.StartSessionAsync(customer.Id, dialog.Notes);
            await ViewModel.LoadActiveSessionsAsync();
        }
    }

    private void OpenDesktopFolder_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.OpenSelectedFolder();
    }

    private void OpenSubfolder_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string sub)
        {
            ViewModel.OpenSubfolder(sub);
        }
    }

    private async void FinishSession_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedSession != null)
        {
            await ViewModel.CompleteSessionAsync(ViewModel.SelectedSession.Session.Id);
        }
    }
}
