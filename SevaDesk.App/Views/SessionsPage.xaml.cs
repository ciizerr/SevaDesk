using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SevaDesk_App.ViewModels;
using SevaDesk.Core.Models;

namespace SevaDesk_App.Views;

public sealed partial class SessionsPage : Page
{
    public SessionsViewModel ViewModel { get; } = new();

    public SessionsPage()
    {
        InitializeComponent();
        ViewModel.Initialize();
    }

    private void Refresh_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.LoadSessions();
    }

    private async void NewSession_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new NewCustomerDialog
        {
            XamlRoot = this.XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(dialog.CustomerName))
        {
            var customer = await Services.AppServices.Customers.CreateAsync(new Customer
            {
                Name = dialog.CustomerName,
                Mobile = string.IsNullOrWhiteSpace(dialog.Mobile) ? null : dialog.Mobile,
                Village = string.IsNullOrWhiteSpace(dialog.Village) ? null : dialog.Village,
                IdReference = string.IsNullOrWhiteSpace(dialog.IdRef) ? null : dialog.IdRef,
                Notes = string.IsNullOrWhiteSpace(dialog.Notes) ? null : dialog.Notes
            });

            var newSession = await Services.AppServices.Sessions.StartSessionAsync(customer.Id, dialog.Notes);
            var folder = Services.AppServices.FolderManager.EnsureCustomerWorkingFolder(customer.Name, customer.Code);
            newSession.FolderPath = folder;
            newSession.Customer = customer;
            newSession.FolderStats = new FolderStats();

            ViewModel.Sessions.Insert(0, newSession);
            ViewModel.SelectedSession = newSession;
            ViewModel.ActiveCount = ViewModel.Sessions.Count(s => s.Session.Status == "Active");
        }
    }

    private void ToggleStatus_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedSession != null)
        {
            ViewModel.ToggleSessionStatusCommand.Execute(ViewModel.SelectedSession);
        }
    }

    private void CompleteSession_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedSession != null)
        {
            ViewModel.CompleteSessionCommand.Execute(ViewModel.SelectedSession);
        }
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedSession != null)
        {
            ViewModel.OpenFolderCommand.Execute(ViewModel.SelectedSession.FolderPath);
        }
    }
}
