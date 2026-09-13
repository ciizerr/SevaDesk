using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SevaDesk_App.Services;
using SevaDesk_App.ViewModels;

namespace SevaDesk_App.Views;

public sealed partial class CustomersPage : Page
{
    public CustomersViewModel ViewModel { get; } = new();

    public CustomersPage()
    {
        InitializeComponent();
        Loaded += async (s, e) => await ViewModel.InitializeAsync();
    }

    private async void AddCustomer_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new NewCustomerDialog
        {
            XamlRoot = XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            var customer = await ViewModel.CreateCustomerAsync(
                dialog.CustomerName,
                dialog.Mobile,
                dialog.Village,
                dialog.IdRef,
                dialog.Notes
            );

            await AppServices.Sessions.StartSessionAsync(customer.Id, dialog.Notes);
        }
    }

    private async void StartSession_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string customerId)
        {
            await AppServices.Sessions.StartSessionAsync(customerId);
            Frame.Navigate(typeof(DashboardPage));
        }
    }
}
