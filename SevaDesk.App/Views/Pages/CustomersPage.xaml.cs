using System;
using System.IO;
using System.Linq;
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

    private readonly Action<string> _avatarUpdatedHandler;

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
        Customer? customer = null;
        if (sender is FrameworkElement el)
        {
            if (el.Tag is Customer c) customer = c;
            else if (el.Tag is CustomerRowModel r) customer = r.Customer;
            else if (el.DataContext is CustomerRowModel crm) customer = crm.Customer;
        }

        if (customer != null)
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
        string? customerId = null;
        if (sender is FrameworkElement el)
        {
            if (el.Tag is string cid) customerId = cid;
            else if (el.Tag is Customer c) customerId = c.Id;
            else if (el.Tag is CustomerRowModel r) customerId = r.Customer.Id;
            else if (el.DataContext is CustomerRowModel crm) customerId = crm.Customer.Id;
        }

        if (!string.IsNullOrWhiteSpace(customerId))
        {
            await AppServices.Sessions.StartSessionAsync(customerId);
            MainWindow.Instance?.NavigateTo(typeof(DashboardPage));
        }
    }

    private async void WhatsApp_Click(object sender, RoutedEventArgs e)
    {
        CustomerRowModel? row = null;
        if (sender is FrameworkElement el)
        {
            if (el.Tag is CustomerRowModel r) row = r;
            else if (el.DataContext is CustomerRowModel crm) row = crm;
        }

        if (row == null) return;

        var digits = row.DigitsOnlyMobile;
        if (digits.Length < 10)
        {
            MainWindow.Instance?.ShowToast("Customer does not have a valid 10-digit mobile number for WhatsApp.", InfoBarSeverity.Warning);
            return;
        }

        string fullPhone = digits.Length == 10 ? "91" + digits : digits;

        try
        {
            // 1. Try launching native desktop WhatsApp app first
            var appUri = new Uri($"whatsapp://send?phone={fullPhone}");
            var launchedApp = await Windows.System.Launcher.LaunchUriAsync(appUri);

            // 2. If native app didn't launch or isn't installed, fallback to WhatsApp Web
            if (!launchedApp)
            {
                var webUri = new Uri($"https://wa.me/{fullPhone}");
                await Windows.System.Launcher.LaunchUriAsync(webUri);
            }
        }
        catch
        {
            // Fallback to web browser wa.me link
            try
            {
                var webUri = new Uri($"https://wa.me/{fullPhone}");
                await Windows.System.Launcher.LaunchUriAsync(webUri);
            }
            catch (Exception ex)
            {
                MainWindow.Instance?.ShowToast($"Could not launch WhatsApp: {ex.Message}", InfoBarSeverity.Error);
            }
        }
    }

    private void CopyPhone_Click(object sender, RoutedEventArgs e)
    {
        Customer? customer = null;
        if (sender is FrameworkElement el)
        {
            if (el.Tag is Customer c) customer = c;
            else if (el.Tag is CustomerRowModel r) customer = r.Customer;
            else if (el.DataContext is CustomerRowModel crm) customer = crm.Customer;
        }

        if (customer != null && !string.IsNullOrWhiteSpace(customer.Mobile))
        {
            var dp = new Windows.ApplicationModel.DataTransfer.DataPackage();
            dp.SetText(customer.Mobile.Trim());
            Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dp);
            MainWindow.Instance?.ShowToast($"Copied {customer.Mobile.Trim()} to clipboard.", InfoBarSeverity.Informational);
        }
    }

    private void OpenProfile_Click(object sender, RoutedEventArgs e)
    {
        Customer? customer = null;
        if (sender is FrameworkElement el)
        {
            if (el.Tag is Customer c) customer = c;
            else if (el.Tag is CustomerRowModel r) customer = r.Customer;
            else if (el.DataContext is CustomerRowModel crm) customer = crm.Customer;
        }

        if (customer != null)
        {
            MainWindow.Instance?.NavigateTo(typeof(CustomerWorkspacePage), customer);
        }
    }

    private void ListView_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is CustomerRowModel row)
        {
            MainWindow.Instance?.NavigateTo(typeof(CustomerWorkspacePage), row.Customer);
        }
    }

    private async void DeleteCustomer_Click(object sender, RoutedEventArgs e)
    {
        Customer? customer = null;
        if (sender is FrameworkElement el)
        {
            if (el.Tag is Customer c) customer = c;
            else if (el.Tag is CustomerRowModel r) customer = r.Customer;
            else if (el.DataContext is CustomerRowModel crm) customer = crm.Customer;
        }

        if (customer != null)
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

            // 5. Reload view model list and counters
            await ViewModel.LoadCustomersAsync();
            await ViewModel.LoadOverviewMetricsAsync();

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
        catch (Exception ex)
        {
            ViewModel.ShowError($"Error deleting customer: {ex.Message}");
        }
    }

    private void FilterRadio_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton rb && rb.Tag is string tagStr && int.TryParse(tagStr, out int filterIdx))
        {
            ViewModel.SetFilter((CustomerFilterMode)filterIdx);
        }
    }

    private void InactivityCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox cb)
        {
            var filter = cb.SelectedIndex switch
            {
                1 => CustomerInactivityFilter.OlderThan1Year,
                2 => CustomerInactivityFilter.OlderThan2Years,
                3 => CustomerInactivityFilter.OlderThan3Years,
                4 => CustomerInactivityFilter.OlderThan4Years,
                5 => CustomerInactivityFilter.OlderThan5Years,
                _ => CustomerInactivityFilter.Any
            };
            ViewModel.SetInactivityFilter(filter);
        }
    }

    private void SortCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox cb && cb.SelectedIndex >= 0)
        {
            ViewModel.SetSort((CustomerSortMode)cb.SelectedIndex);
        }
    }

    private void PageSizeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox cb && cb.SelectedItem is ComboBoxItem item && int.TryParse(item.Content?.ToString(), out int size))
        {
            ViewModel.SetPageSize(size);
        }
    }

    private void ClearFilters_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.SearchQuery = string.Empty;
        ViewModel.SetFilter(CustomerFilterMode.All);
        ViewModel.SetInactivityFilter(CustomerInactivityFilter.Any);
        if (FilterAllRadio != null)
        {
            FilterAllRadio.IsChecked = true;
        }
        if (InactivityCombo != null)
        {
            InactivityCombo.SelectedIndex = 0;
        }
    }
}

