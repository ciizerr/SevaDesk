using System.IO;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using SevaDesk.Core.Models;
using SevaDesk_App.Services;
using SevaDesk_App.ViewModels.Pages;
using SevaDesk_App.Views.Dialogs;

namespace SevaDesk_App.Views.Pages;

public sealed partial class CustomersPage : Page
{
    public CustomersViewModel ViewModel { get; } = new();

    private readonly System.Action<string> _avatarUpdatedHandler;

    public CustomersPage()
    {
        InitializeComponent();
        _avatarUpdatedHandler = (photo) =>
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                foreach (var row in ViewModel.Customers)
                {
                    row.Customer.NotifyPhotoUpdated();
                }
            });
        };
        Loaded += async (s, e) =>
        {
            await ViewModel.InitializeAsync();
            CustomerAvatarHelper.AvatarUpdated += _avatarUpdatedHandler;
        };
        Unloaded += (s, e) =>
        {
            CustomerAvatarHelper.AvatarUpdated -= _avatarUpdatedHandler;
        };
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

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement el && el.Tag is Customer customer)
        {
            var folderPath = AppServices.FolderManager.GetEffectiveCustomerFolderPath(
                customer.Name, customer.Code, out _);

            if (!Directory.Exists(folderPath))
            {
                folderPath = AppServices.FolderManager.EnsureCustomerWorkingFolder(
                    customer.Name, customer.Code);
            }

            AppServices.FolderManager.OpenFolderInExplorer(folderPath);
        }
    }

    private async void StartSession_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement el && el.Tag is string customerId)
        {
            await AppServices.Sessions.StartSessionAsync(customerId);
            MainWindow.Instance?.NavigateTo(typeof(DashboardPage));
        }
    }

    private async void DeleteCustomer_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement el && el.Tag is Customer customer)
        {
            await DeleteCustomerWorkflowAsync(customer);
        }
    }

    private async System.Threading.Tasks.Task DeleteCustomerWorkflowAsync(Customer customer)
    {
        // 1. Safeguard: Check if customer has an active or paused session
        var activeSessions = await AppServices.Sessions.GetActiveSessionsAsync();
        if (activeSessions.Any(s => s.Customer?.Id == customer.Id || s.Session.CustomerId == customer.Id))
        {
            await AppServices.Dialogs.ShowAlertAsync(
                "Active Session Running",
                $"An active or paused session is currently in progress for '{customer.Name}'.\n\nPlease complete or delete the ongoing session first before deleting this customer profile.");
            return;
        }

        // 2. Prompt confirmation dialog
        var action = await AppServices.Dialogs.PromptDeleteCustomerAsync(customer, XamlRoot);
        if (action == DeleteCustomerAction.Cancel) return;

        try
        {
            // 3. Clean up disk folders according to choice
            AppServices.FolderManager.DeleteCustomerWorkingFolder(customer.Name, customer.Code);
            if (action == DeleteCustomerAction.DeleteAndWipeAll)
            {
                AppServices.FolderManager.DeleteCustomerBackupFolder(customer.Name, customer.Code);
            }

            // 4. Delete from database
            await AppServices.Customers.DeleteCustomerAsync(customer.Id);

            // 5. Remove from UI list
            var existing = ViewModel.Customers.FirstOrDefault(c => c.Customer.Id == customer.Id);
            if (existing != null)
            {
                ViewModel.Customers.Remove(existing);
                ViewModel.TotalCustomersCount = ViewModel.Customers.Count;
            }

            // 6. Toast feedback
            if (action == DeleteCustomerAction.DeleteAndWipeAll)
            {
                MainWindow.Instance?.ShowToast($"Customer '{customer.Name}' and all files permanently deleted.", InfoBarSeverity.Success);
            }
            else
            {
                MainWindow.Instance?.ShowToast($"Customer '{customer.Name}' deleted. Backup files preserved.", InfoBarSeverity.Success);
            }
        }
        catch (System.Exception ex)
        {
            ViewModel.ShowError($"Error deleting customer: {ex.Message}");
        }
    }

    private void ListView_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is CustomerRowModel row)
        {
            MainWindow.Instance?.NavigateTo(typeof(CustomerWorkspacePage), row.Customer);
        }
    }

    private void Row_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (sender is FrameworkElement el && el.DataContext is CustomerRowModel row)
        {
            _ = row.LoadStatsAsync();
        }
    }
}
