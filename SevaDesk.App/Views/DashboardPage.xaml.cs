using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SevaDesk.Core.Models;
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
                var matches = (await AppServices.Customers.SearchAsync(dialog.CustomerName)).ToList();
                var exact = matches.FirstOrDefault(c => string.Equals(c.Name, dialog.CustomerName, System.StringComparison.OrdinalIgnoreCase));
                if (exact != null)
                {
                    customer = exact;
                }
                else
                {
                    customer = await AppServices.Customers.CreateAsync(new Customer
                    {
                        Name = dialog.CustomerName,
                        Mobile = string.IsNullOrWhiteSpace(dialog.Mobile) ? null : dialog.Mobile,
                        Village = string.IsNullOrWhiteSpace(dialog.Village) ? null : dialog.Village,
                        IdReference = string.IsNullOrWhiteSpace(dialog.IdRef) ? null : dialog.IdRef,
                        Notes = string.IsNullOrWhiteSpace(dialog.Notes) ? null : dialog.Notes
                    });
                }
            }

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
            var customer = ViewModel.SelectedSession.Customer;
            if (customer != null && (string.IsNullOrWhiteSpace(customer.Mobile) || string.IsNullOrWhiteSpace(customer.IdReference)))
            {
                var dialog = new CompleteSessionDialog(customer)
                {
                    XamlRoot = XamlRoot
                };

                var res = await dialog.ShowAsync();
                if (res == ContentDialogResult.Primary)
                {
                    dialog.ApplyToCustomer(customer);
                    await AppServices.Customers.UpdateAsync(customer);
                }
                else if (res != ContentDialogResult.Secondary)
                {
                    // User canceled
                    return;
                }
            }

            await ViewModel.CompleteSessionAsync(ViewModel.SelectedSession.Session.Id);
        }
    }
}
