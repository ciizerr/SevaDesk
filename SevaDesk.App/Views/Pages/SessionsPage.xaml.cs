using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SevaDesk_App.ViewModels.Pages;
using SevaDesk.Core.Models;
using SevaDesk_App.Services;
using SevaDesk_App.Views.Dialogs;

namespace SevaDesk_App.Views.Pages;

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

            var newSession = await AppServices.Sessions.StartSessionAsync(customer.Id, dialog.Notes);
            var folder = AppServices.FolderManager.EnsureCustomerWorkingFolder(customer.Name, customer.Code);
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

    private async void CompleteSession_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedSession != null)
        {
            var customer = ViewModel.SelectedSession.Customer;
            if (customer != null && (string.IsNullOrWhiteSpace(customer.Mobile) || string.IsNullOrWhiteSpace(customer.IdReference)))
            {
                var dialog = new CompleteSessionDialog(customer)
                {
                    XamlRoot = this.XamlRoot
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

            await AppServices.Sessions.CompleteSessionAsync(ViewModel.SelectedSession.Session.Id);
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

    private void OpenSubfolderTile_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string subfolder)
        {
            ViewModel.OpenSubfolderCommand.Execute(subfolder);
        }
    }

    private void OpenAppFolder_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string subfolder)
        {
            ViewModel.OpenSubfolderCommand.Execute(subfolder);
        }
    }

    private async void AddAppFolder_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedSession == null) return;

        var txtName = new TextBox
        {
            PlaceholderText = "e.g. PMS, SSC_CGL, NSP, GDS",
            Margin = new Thickness(0, 8, 0, 0)
        };

        var dialog = new ContentDialog
        {
            XamlRoot = this.XamlRoot,
            Title = Services.AppServices.Localization.GetString("Sessions.CreateFolderDialogTitle"),
            Content = new StackPanel
            {
                Spacing = 6,
                Children =
                {
                    new TextBlock { Text = Services.AppServices.Localization.GetString("Sessions.CreateFolderDialogPrompt") },
                    txtName
                }
            },
            PrimaryButtonText = Services.AppServices.Localization.GetString("Common.Add"),
            CloseButtonText = Services.AppServices.Localization.GetString("Common.Cancel"),
            DefaultButton = ContentDialogButton.Primary
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(txtName.Text))
        {
            ViewModel.CreateApplicationFolderCommand.Execute(txtName.Text.Trim());
        }
    }

    private async void RoutePendingFile_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.RoutePendingFileCommand.ExecuteAsync(null);
    }

    private void TriageClose_Click(InfoBar sender, object args)
    {
        ViewModel.DismissPendingFileCommand.Execute(null);
    }

    private void DismissPendingFile_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.DismissPendingFileCommand.Execute(null);
    }
}
