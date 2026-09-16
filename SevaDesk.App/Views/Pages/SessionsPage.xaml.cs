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

    private readonly DispatcherTimer _elapsedTimer = new() { Interval = System.TimeSpan.FromSeconds(1) };

    public SessionsPage()
    {
        InitializeComponent();
        _elapsedTimer.Tick += (s, e) =>
        {
            ViewModel.SelectedSession?.UpdateElapsed();
        };

        ViewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(ViewModel.SelectedSession))
            {
                ViewModel.SelectedSession?.UpdateElapsed();
                WorkingFolderBrowser.LoadCustomerFolder(ViewModel.SelectedSession?.FolderPath ?? string.Empty);
            }
        };
        Loaded += (s, e) =>
        {
            ViewModel.SelectedSession?.UpdateElapsed();
            WorkingFolderBrowser.LoadCustomerFolder(ViewModel.SelectedSession?.FolderPath ?? string.Empty);
            _elapsedTimer.Start();
        };
        Unloaded += (s, e) =>
        {
            _elapsedTimer.Stop();
        };
        ViewModel.Initialize();
    }

    public static Visibility VisibleIf(bool condition) => condition ? Visibility.Visible : Visibility.Collapsed;
    public static Visibility CollapsedIf(bool condition) => condition ? Visibility.Collapsed : Visibility.Visible;
    public static string CollapseGlyph(bool isCollapsed) => isCollapsed ? "\uE70D" : "\uE70E";
    public static string CollapseToolTip(bool isCollapsed) => isCollapsed ? "Expand Details" : "Collapse Details";

    private void ToggleAppCollapse_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.ToggleApplicationCardCollapse();
    }

    private void AppChip_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is ApplicationItem app)
        {
            ViewModel.SelectApplication(app);
        }
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.LoadSessionsAsync();
        ViewModel.SelectedSession?.UpdateElapsed();
        WorkingFolderBrowser.LoadCustomerFolder(ViewModel.SelectedSession?.FolderPath ?? string.Empty);
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

    private async void ToggleStatus_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedSession != null)
        {
            await ViewModel.ToggleSessionStatusAsync(ViewModel.SelectedSession);
        }
    }

    private async void CompleteSession_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedSession != null)
        {
            var currentSession = ViewModel.SelectedSession;
            var customer = currentSession.Customer;
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

            // Prepare handover request if an active citizen application exists
            BillingHandoverRequest? handover = null;
            if (ViewModel.ActiveApplication != null)
            {
                var app = ViewModel.ActiveApplication;
                handover = new BillingHandoverRequest
                {
                    CustomerName = customer?.Name ?? "Customer",
                    CustomerId = customer?.Id,
                    SessionId = currentSession.Session.Id,
                    Items = []
                };

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

                if (handover.Items.Count == 0)
                {
                    handover.Items.Add(new CartItem
                    {
                        ServiceName = $"{app.Title} Form Fill",
                        Rate = 100,
                        Quantity = 1
                    });
                }
            }

            await ViewModel.CompleteSessionAsync(currentSession);

            if (handover != null && MainWindow.Instance != null)
            {
                MainWindow.Instance.NavigateTo(typeof(PaymentsPage), handover);
            }
        }
    }

    private async void LinkApplication_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedSession?.Customer == null) return;
        var customer = ViewModel.SelectedSession.Customer;
        var dialog = new LinkApplicationDialog(customer.Name, customer.Id)
        {
            XamlRoot = this.XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary || dialog.IsConfirmed)
        {
            var app = dialog.BuildApplication();
            await ViewModel.LinkApplicationCommand.ExecuteAsync(app);
        }
    }

    private async void LaunchPortal_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.ActiveApplication != null)
        {
            await ViewModel.OpenPortalUrlCommand.ExecuteAsync(ViewModel.ActiveApplication.PortalName);
        }
    }

    private async void SetDraft_Click(object sender, RoutedEventArgs e) => await ViewModel.UpdateApplicationStatusCommand.ExecuteAsync("Draft");
    private async void SetDocsReady_Click(object sender, RoutedEventArgs e) => await ViewModel.UpdateApplicationStatusCommand.ExecuteAsync("Docs Uploaded");
    private async void SetSubmitted_Click(object sender, RoutedEventArgs e) => await ViewModel.UpdateApplicationStatusCommand.ExecuteAsync("Submitted");
    private async void SetCompleted_Click(object sender, RoutedEventArgs e) => await ViewModel.UpdateApplicationStatusCommand.ExecuteAsync("Completed");

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

        var (result, rawName) = await Services.AppServices.Dialogs.ShowInputAsync(
            Services.AppServices.Localization.GetString("Sessions.CreateFolderDialogTitle"),
            Services.AppServices.Localization.GetString("Sessions.CreateFolderDialogPrompt"),
            "e.g. PMS, SSC_CGL, NSP, GDS",
            Services.AppServices.Localization.GetString("Common.Add"),
            Services.AppServices.Localization.GetString("Common.Cancel")
        );

        if (result == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(rawName))
        {
            ViewModel.CreateApplicationFolderCommand.Execute(rawName.Trim());
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
