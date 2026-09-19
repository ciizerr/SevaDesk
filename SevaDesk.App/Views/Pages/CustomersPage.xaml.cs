using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SevaDesk.Core.Models;
using SevaDesk_App.Services;
using SevaDesk_App.ViewModels.Pages;
using SevaDesk_App.Views.Dialogs;

namespace SevaDesk_App.Views.Pages;

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
        if (result == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(dialog.CustomerName))
        {
            Customer customer;
            if (dialog.SelectedExistingCustomer != null)
            {
                customer = dialog.SelectedExistingCustomer;
                bool changed = false;
                if (!string.IsNullOrWhiteSpace(dialog.Mobile) && dialog.Mobile != customer.Mobile)
                {
                    customer.Mobile = dialog.Mobile;
                    changed = true;
                }
                if (!string.IsNullOrWhiteSpace(dialog.Village) && dialog.Village != customer.Village)
                {
                    customer.Village = dialog.Village;
                    changed = true;
                }
                if (!string.IsNullOrWhiteSpace(dialog.IdRef) && dialog.IdRef != customer.IdReference)
                {
                    customer.IdReference = dialog.IdRef;
                    changed = true;
                }
                if (changed)
                {
                    await AppServices.Customers.UpdateAsync(customer);
                }
            }
            else
            {
                customer = await ViewModel.CreateCustomerAsync(
                    dialog.CustomerName,
                    dialog.Mobile,
                    dialog.Village,
                    dialog.IdRef,
                    dialog.Notes
                );
            }

            await AppServices.Sessions.StartSessionAsync(customer.Id, dialog.Notes);
            await ViewModel.LoadCustomersAsync();
        }
    }

    private async void StartSession_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string customerId)
        {
            await AppServices.Sessions.StartSessionAsync(customerId);
            MainWindow.Instance?.NavigateTo(typeof(DashboardPage));
        }
    }

    private void ListView_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is Customer customer)
        {
            MainWindow.Instance?.NavigateTo(typeof(CustomerWorkspacePage), customer);
        }
    }
}
