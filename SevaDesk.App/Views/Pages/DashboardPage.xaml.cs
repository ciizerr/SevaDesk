using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using SevaDesk.Core.Models;
using SevaDesk_App.Services;
using SevaDesk_App.ViewModels.Pages;
using SevaDesk_App.Views.Dialogs;

namespace SevaDesk_App.Views.Pages;

public sealed partial class DashboardPage : Page
{
    public DashboardViewModel ViewModel { get; } = new();

    private readonly DispatcherTimer _elapsedTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly Action<string> _avatarUpdatedHandler;

    public DashboardPage()
    {
        InitializeComponent();

        _elapsedTimer.Tick += (s, e) =>
        {
            foreach (var session in ViewModel.ActiveSessions)
            {
                session.UpdateElapsed();
            }
        };

        _avatarUpdatedHandler = (photo) =>
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                foreach (var s in ViewModel.ActiveSessions)
                {
                    s.Customer?.NotifyPhotoUpdated();
                }
            });
        };

        Loaded += async (s, e) =>
        {
            await ViewModel.InitializeAsync();
            CustomerAvatarHelper.AvatarUpdated += _avatarUpdatedHandler;
            _elapsedTimer.Start();
        };

        Unloaded += (s, e) =>
        {
            CustomerAvatarHelper.AvatarUpdated -= _avatarUpdatedHandler;
            _elapsedTimer.Stop();
        };
    }

    public static Visibility VisibleIf(bool condition) => condition ? Visibility.Visible : Visibility.Collapsed;
    public static Visibility CollapsedIf(bool condition) => condition ? Visibility.Collapsed : Visibility.Visible;

    public static Brush SessionBadgeBackground(string? status) =>
        status == "Paused"
            ? new SolidColorBrush(Color.FromArgb(35, 245, 158, 11)) // Amber
            : new SolidColorBrush(Color.FromArgb(35, 16, 185, 129)); // Emerald

    public static Brush SessionBadgeForeground(string? status) =>
        status == "Paused"
            ? new SolidColorBrush(Color.FromArgb(255, 217, 119, 6))
            : new SolidColorBrush(Color.FromArgb(255, 16, 185, 129));

    public static string SessionBadgeGlyph(string? status) =>
        status == "Paused" ? "\uE769" : "\uE73E";

    public static Brush AppStatusBackground(string? status) =>
        status switch
        {
            "Docs Ready" or "Docs Uploaded" => new SolidColorBrush(Color.FromArgb(35, 59, 130, 246)),
            "Completed" => new SolidColorBrush(Color.FromArgb(35, 16, 185, 129)),
            _ => new SolidColorBrush(Color.FromArgb(35, 245, 158, 11))
        };

    public static Brush AppStatusForeground(string? status) =>
        status switch
        {
            "Docs Ready" or "Docs Uploaded" => new SolidColorBrush(Color.FromArgb(255, 59, 130, 246)),
            "Completed" => new SolidColorBrush(Color.FromArgb(255, 16, 185, 129)),
            _ => new SolidColorBrush(Color.FromArgb(255, 217, 119, 6))
        };

    public static Brush PaymentModeBadgeBackground(string? mode) =>
        string.Equals(mode, "Cash", StringComparison.OrdinalIgnoreCase)
            ? new SolidColorBrush(Color.FromArgb(30, 16, 185, 129))
            : new SolidColorBrush(Color.FromArgb(30, 139, 92, 246));

    public static Brush PaymentModeBadgeForeground(string? mode) =>
        string.Equals(mode, "Cash", StringComparison.OrdinalIgnoreCase)
            ? new SolidColorBrush(Color.FromArgb(255, 16, 185, 129))
            : new SolidColorBrush(Color.FromArgb(255, 139, 92, 246));

    public static string FormatRupee(decimal amount) => $"₹{amount:N0}";

    public static string FormatPaymentTime(DateTime dt) => dt.ToLocalTime().ToString("hh:mm tt");

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.LoadDashboardDataAsync();
    }

    private void TogglePrivacy_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.TogglePrivacyMode();
    }

    private void NewBill_Click(object sender, RoutedEventArgs e)
    {
        MainWindow.Instance?.NavigateTo(typeof(PaymentsPage));
    }

    private void ViewAllSessions_Click(object sender, RoutedEventArgs e)
    {
        MainWindow.Instance?.NavigateTo(typeof(SessionsPage));
    }

    private void ViewAllApplications_Click(object sender, RoutedEventArgs e)
    {
        MainWindow.Instance?.NavigateTo(typeof(ApplicationsPage));
    }

    private void ViewCashier_Click(object sender, RoutedEventArgs e)
    {
        MainWindow.Instance?.NavigateTo(typeof(PaymentsPage));
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
                var exact = matches.FirstOrDefault(c => string.Equals(c.Name, dialog.CustomerName, StringComparison.OrdinalIgnoreCase));
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
            await ViewModel.LoadDashboardDataAsync();
        }
    }

    private void SessionFolder_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement el && el.Tag is ActiveSessionItem item)
        {
            ViewModel.OpenFolder(item);
        }
    }

    private async void SessionCompleteAndBill_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement el || el.Tag is not ActiveSessionItem currentSession)
            return;

        var customer = currentSession.Customer;
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

        // Prepare handover request for Billing
        var handover = new BillingHandoverRequest
        {
            CustomerName = customer?.Name ?? "Customer",
            CustomerId = customer?.Id,
            SessionId = currentSession.Session.Id,
            Items = []
        };

        if (currentSession.LinkedApplication != null)
        {
            var app = currentSession.LinkedApplication;
            if (app.ServiceCharge > 0)
            {
                handover.Items.Add(new CartItem
                {
                    ServiceName = $"{app.Title} (Service Fee)",
                    Rate = app.ServiceCharge,
                    Quantity = 1
                });
            }

            if (app.GovtFee > 0)
            {
                handover.Items.Add(new CartItem
                {
                    ServiceName = $"{app.Title} (Govt Fee)",
                    Rate = app.GovtFee,
                    Quantity = 1
                });
            }
        }

        // Complete the session
        await ViewModel.CompleteSessionAsync(currentSession.Session.Id);

        // Navigate directly to Cashier with billing handover
        MainWindow.Instance?.NavigateTo(typeof(PaymentsPage), handover);
    }

    private async void SessionTogglePause_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement el && el.Tag is ActiveSessionItem session)
        {
            if (session.IsPaused)
            {
                await ViewModel.ResumeSessionAsync(session.Session.Id);
            }
            else
            {
                await ViewModel.PauseSessionAsync(session.Session.Id);
            }
        }
    }

    private void SessionOpenInConsole_Click(object sender, RoutedEventArgs e)
    {
        MainWindow.Instance?.NavigateTo(typeof(SessionsPage));
    }

    private async void SessionDelete_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement el || el.Tag is not ActiveSessionItem current)
            return;

        var (action, fileCount) = await AppServices.Dialogs.PromptDeleteActiveSessionAsync(current, XamlRoot);
        if (action == DeleteSessionFileAction.Cancel) return;

        if (action == DeleteSessionFileAction.BackupAndDelete && current.Customer != null)
        {
            try
            {
                await AppServices.FolderManager.SyncToBackupAsync(current.Customer.Name, current.Customer.Code);
                AppServices.FolderManager.DeleteCustomerWorkingFolder(current.Customer.Name, current.Customer.Code);
            }
            catch { }
        }

        await ViewModel.DeleteSessionCommand.ExecuteAsync(current);

        if (action == DeleteSessionFileAction.BackupAndDelete)
        {
            MainWindow.Instance?.ShowToast(
                fileCount > 0
                    ? $"Session deleted. {fileCount} file(s) backed up to permanent archive and removed from Desktop."
                    : "Session deleted and Desktop folder cleaned up.",
                InfoBarSeverity.Success);
        }
        else if (fileCount > 0)
        {
            MainWindow.Instance?.ShowToast(
                "Session deleted. Files kept on Desktop.",
                InfoBarSeverity.Informational);
        }
        else
        {
            MainWindow.Instance?.ShowToast(
                AppServices.Localization.GetString("Toast.DeleteSuccess", "Session deleted"),
                InfoBarSeverity.Success);
        }
    }

    private async void ApplicationOpenWorkspace_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement el && el.Tag is ApplicationItem app && !string.IsNullOrWhiteSpace(app.CustomerId))
        {
            var customer = await AppServices.Customers.GetByIdAsync(app.CustomerId);
            if (customer != null)
            {
                MainWindow.Instance?.NavigateTo(typeof(CustomerWorkspacePage), customer);
            }
            else
            {
                MainWindow.Instance?.NavigateTo(typeof(ApplicationsPage));
            }
        }
        else
        {
            MainWindow.Instance?.NavigateTo(typeof(ApplicationsPage));
        }
    }
}
