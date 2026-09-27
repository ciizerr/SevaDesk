using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Windows.UI;
using SevaDesk_App.ViewModels.Pages;
using SevaDesk.Core.Models;
using SevaDesk_App.Services;
using SevaDesk_App.Views.Dialogs;

namespace SevaDesk_App.Views.Pages;

public sealed partial class SessionsPage : Page
{
    public SessionsViewModel ViewModel { get; } = new();

    private readonly DispatcherTimer _elapsedTimer = new() { Interval = System.TimeSpan.FromSeconds(1) };

    private readonly Action<string> _avatarUpdatedHandler;

    private bool _isStackedMode;
    private bool? _userLayoutOverride; // null = Auto, true = Stacked, false = SideBySide
    private bool _isSchemeCardCollapsed;

    public SessionsPage()
    {
        InitializeComponent();
        _elapsedTimer.Tick += (s, e) =>
        {
            ViewModel.SelectedSession?.UpdateElapsed();
            foreach (var session in ViewModel.Sessions)
            {
                session.UpdateElapsed();
            }
        };

        ViewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(ViewModel.SelectedSession))
            {
                ViewModel.SelectedSession?.UpdateElapsed();
                WorkingFolderBrowser.LoadCustomerFolder(ViewModel.SelectedSession?.FolderPath ?? string.Empty, ViewModel.SelectedSession?.Customer);
                ApplyLayoutMode();
            }
            else if (e.PropertyName == nameof(ViewModel.HasActiveApplication))
            {
                ApplyLayoutMode();
            }
        };

        _avatarUpdatedHandler = (photo) =>
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                if (ViewModel.SelectedSession?.Customer != null)
                {
                    ViewModel.SelectedSession.Customer.NotifyPhotoUpdated();
                }
                foreach (var s in ViewModel.Sessions)
                {
                    s.Customer.NotifyPhotoUpdated();
                }
            });
        };

        Loaded += (s, e) =>
        {
            ViewModel.SelectedSession?.UpdateElapsed();
            WorkingFolderBrowser.LoadCustomerFolder(ViewModel.SelectedSession?.FolderPath ?? string.Empty, ViewModel.SelectedSession?.Customer);
            CustomerAvatarHelper.AvatarUpdated += _avatarUpdatedHandler;
            _elapsedTimer.Start();
            ApplyLayoutMode();
        };
        Unloaded += (s, e) =>
        {
            CustomerAvatarHelper.AvatarUpdated -= _avatarUpdatedHandler;
            _elapsedTimer.Stop();
        };

        WorkingFolderBrowser.FilesChanged += () =>
        {
            _ = ViewModel.RefreshActiveChecklistAsync();
            ViewModel.RefreshApplicationFolders();
            if (ViewModel.SelectedSession != null)
            {
                ViewModel.SelectedSession.FolderStats = AppServices.FolderManager.GetFolderStats(ViewModel.SelectedSession.FolderPath);
                ViewModel.SelectedSession.NotifyStatusChanged();
            }
        };

        WorkingFolderBrowser.CustomerPhotoUpdated += (photo) =>
        {
            if (ViewModel.SelectedSession?.Customer != null)
            {
                ViewModel.SelectedSession.Customer.PhotoPath = photo;
                ViewModel.SelectedSession.Customer.NotifyPhotoUpdated();
            }
        };

        ViewModel.Initialize();
    }

    private void WorkspaceScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (e.NewSize.Width <= 0) return;

        if (_userLayoutOverride == null)
        {
            bool shouldBeStacked = e.NewSize.Width < 950;
            if (shouldBeStacked != _isStackedMode)
            {
                _isStackedMode = shouldBeStacked;
                ApplyLayoutMode();
            }
        }
    }

    private void ToggleLayoutMode_Click(object sender, RoutedEventArgs e)
    {
        _userLayoutOverride = !_isStackedMode;
        _isStackedMode = _userLayoutOverride.Value;
        ApplyLayoutMode();
    }

    private void ResetAutoLayout_Click(object sender, RoutedEventArgs e)
    {
        _userLayoutOverride = null;
        var width = WorkspaceScrollViewer?.ActualWidth ?? 0;
        _isStackedMode = width > 0 && width < 950;
        ApplyLayoutMode();
    }

    private void ToggleSchemeCard_Click(object sender, RoutedEventArgs e)
    {
        _isSchemeCardCollapsed = !_isSchemeCardCollapsed;
        if (AppCardBodyGrid != null)
            AppCardBodyGrid.Visibility = _isSchemeCardCollapsed ? Visibility.Collapsed : Visibility.Visible;
        if (AppCardSummaryChip != null)
            AppCardSummaryChip.Visibility = _isSchemeCardCollapsed ? Visibility.Visible : Visibility.Collapsed;
        if (IconToggleSchemeCard != null)
            IconToggleSchemeCard.Glyph = _isSchemeCardCollapsed ? "\uE70E" : "\uE70D";
        if (BtnToggleSchemeCard != null)
            ToolTipService.SetToolTip(BtnToggleSchemeCard, _isSchemeCardCollapsed
                ? AppServices.Localization.GetString("Sessions.ExpandScheme", "Expand scheme details")
                : AppServices.Localization.GetString("Sessions.CollapseScheme", "Collapse scheme details"));
    }

    private void ApplyLayoutMode()
    {
        if (WorkspaceGrid == null || AppCardBorder == null || WorkingFolderBrowser == null || AppCardBodyGrid == null)
            return;

        bool hasApp = ViewModel.HasActiveApplication;

        if (_isStackedMode)
        {
            // 1. Configure WorkspaceGrid as Rows (Stacked vertically)
            WorkspaceGrid.ColumnDefinitions.Clear();
            WorkspaceGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            WorkspaceGrid.RowDefinitions.Clear();
            WorkspaceGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            WorkspaceGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            WorkspaceGrid.ColumnSpacing = 0;
            WorkspaceGrid.RowSpacing = 14;

            Grid.SetColumn(AppCardBorder, 0);
            Grid.SetRow(AppCardBorder, 0);
            Grid.SetColumnSpan(AppCardBorder, 1);

            Grid.SetColumn(WorkingFolderBrowser, 0);
            Grid.SetRow(WorkingFolderBrowser, hasApp ? 1 : 0);
            Grid.SetColumnSpan(WorkingFolderBrowser, 1);

            // 2. Configure AppCardBodyGrid internal 2-column split
            if (AppDetailsSubpane != null && AppChecklistSubpane != null)
            {
                AppCardBodyGrid.ColumnDefinitions.Clear();
                AppCardBodyGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(360) });
                AppCardBodyGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                AppCardBodyGrid.RowDefinitions.Clear();
                AppCardBodyGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                AppCardBodyGrid.ColumnSpacing = 16;
                AppCardBodyGrid.RowSpacing = 0;

                Grid.SetColumn(AppDetailsSubpane, 0);
                Grid.SetRow(AppDetailsSubpane, 0);

                Grid.SetColumn(AppChecklistSubpane, 1);
                Grid.SetRow(AppChecklistSubpane, 0);
            }
        }
        else
        {
            // 1. Configure WorkspaceGrid as Columns (Side-by-side)
            WorkspaceGrid.ColumnDefinitions.Clear();
            WorkspaceGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(380) });
            WorkspaceGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            WorkspaceGrid.RowDefinitions.Clear();
            WorkspaceGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            WorkspaceGrid.ColumnSpacing = 14;
            WorkspaceGrid.RowSpacing = 0;

            Grid.SetColumn(AppCardBorder, 0);
            Grid.SetRow(AppCardBorder, 0);
            Grid.SetColumnSpan(AppCardBorder, 1);

            Grid.SetColumn(WorkingFolderBrowser, hasApp ? 1 : 0);
            Grid.SetRow(WorkingFolderBrowser, 0);
            Grid.SetColumnSpan(WorkingFolderBrowser, hasApp ? 1 : 2);

            // 2. Configure AppCardBodyGrid single-column vertical stack
            if (AppDetailsSubpane != null && AppChecklistSubpane != null)
            {
                AppCardBodyGrid.ColumnDefinitions.Clear();
                AppCardBodyGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                AppCardBodyGrid.RowDefinitions.Clear();
                AppCardBodyGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                AppCardBodyGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                AppCardBodyGrid.ColumnSpacing = 0;
                AppCardBodyGrid.RowSpacing = 12;

                Grid.SetColumn(AppDetailsSubpane, 0);
                Grid.SetRow(AppDetailsSubpane, 0);

                Grid.SetColumn(AppChecklistSubpane, 0);
                Grid.SetRow(AppChecklistSubpane, 1);
            }
        }

        UpdateMenuFlyoutState();
    }

    private void UpdateMenuFlyoutState()
    {
        if (MfiToggleLayout != null)
        {
            if (_isStackedMode)
            {
                MfiToggleLayout.Text = AppServices.Localization.GetString("Sessions.SwitchToSideBySide", "Switch to Side-by-Side View");
                if (IconToggleLayout != null) IconToggleLayout.Glyph = "\uE745";
            }
            else
            {
                MfiToggleLayout.Text = AppServices.Localization.GetString("Sessions.SwitchToStacked", "Switch to Stacked View");
                if (IconToggleLayout != null) IconToggleLayout.Glyph = "\uE746";
            }
        }

        if (MfiResetAutoLayout != null)
        {
            MfiResetAutoLayout.Visibility = _userLayoutOverride != null ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    public static string ChecklistSummary(IEnumerable<ApplicationChecklistItem>? items)
    {
        if (items == null) return "0 Docs";
        var list = items.ToList();
        if (list.Count == 0) return "0 Docs";
        int matched = list.Count(i => i.HasMatchedFile || i.IsCompleted);
        return $"{matched}/{list.Count} Docs Verified";
    }

    public static int BrowserColumn(bool hasApp) => hasApp ? 1 : 0;
    public static int BrowserColumnSpan(bool hasApp) => hasApp ? 1 : 2;

    public static Visibility VisibleIf(bool condition) => condition ? Visibility.Visible : Visibility.Collapsed;
    public static Visibility CollapsedIf(bool condition) => condition ? Visibility.Collapsed : Visibility.Visible;
    public static string CollapseGlyph(bool isCollapsed) => isCollapsed ? "\uE70D" : "\uE70E";

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

    private void OpenCustomerWorkspace_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedSession?.Customer != null)
        {
            MainWindow.Instance?.NavigateTo(typeof(CustomerWorkspacePage), ViewModel.SelectedSession.Customer);
        }
    }

    public static Brush AppStatusBackground(string? status) =>
        status switch
        {
            "Docs Ready" or "Docs Uploaded" => new SolidColorBrush(Color.FromArgb(35, 59, 130, 246)),
            "Completed" => new SolidColorBrush(Color.FromArgb(35, 16, 185, 129)),
            _ => new SolidColorBrush(Color.FromArgb(35, 245, 158, 11)) // Amber for Draft
        };

    public static Brush AppStatusBorder(string? status) =>
        status switch
        {
            "Docs Ready" or "Docs Uploaded" => new SolidColorBrush(Color.FromArgb(200, 59, 130, 246)),
            "Completed" => new SolidColorBrush(Color.FromArgb(200, 16, 185, 129)),
            _ => new SolidColorBrush(Color.FromArgb(200, 245, 158, 11))
        };

    public static Brush AppStatusForeground(string? status) =>
        status switch
        {
            "Docs Ready" or "Docs Uploaded" => new SolidColorBrush(Color.FromArgb(255, 59, 130, 246)),
            "Completed" => new SolidColorBrush(Color.FromArgb(255, 16, 185, 129)),
            _ => new SolidColorBrush(Color.FromArgb(255, 217, 119, 6)) // Amber
        };

    public static string AppStatusGlyph(string? status) =>
        status switch
        {
            "Docs Ready" or "Docs Uploaded" => "\uE73E", // Checkmark
            "Completed" => "\uE930", // Success
            _ => "\uE8A5" // Document
        };

    public static string AppStatusDisplay(string? status) =>
        status switch
        {
            "Docs Ready" or "Docs Uploaded" => "Docs Ready",
            "Completed" => "Completed",
            _ => "Draft"
        };

    public static Brush ChecklistBadgeBackground(bool hasFile) =>
        hasFile
            ? new SolidColorBrush(Color.FromArgb(35, 16, 185, 129))
            : (Brush)Application.Current.Resources["SubtleFillColorTertiaryBrush"];

    public static Brush ChecklistBadgeForeground(bool hasFile) =>
        hasFile
            ? new SolidColorBrush(Color.FromArgb(255, 16, 185, 129))
            : (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"];

    public static string ChecklistBadgeText(bool hasFile, string category) =>
        hasFile ? "Verified" : category;
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
        WorkingFolderBrowser.LoadCustomerFolder(ViewModel.SelectedSession?.FolderPath ?? string.Empty, ViewModel.SelectedSession?.Customer);
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

            ViewModel.PendingPreferredSessionId = customer.Id;
            var newSession = await AppServices.Sessions.StartSessionAsync(customer.Id, dialog.Notes);
            await ViewModel.LoadSessionsAsync(newSession.Session.Id);
        }
    }

    private async void DeleteSession_Click(object sender, RoutedEventArgs e)
    {
        var current = (sender as FrameworkElement)?.DataContext as ActiveSessionItem ?? ViewModel.SelectedSession;
        if (current == null) return;

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


    private async void RemoveApplication_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.ActiveApplication == null) return;
        var app = ViewModel.ActiveApplication;
        var title = app.Title;

        var confirm = await AppServices.Dialogs.ShowConfirmationAsync(
            "Remove Linked Application",
            $"Are you sure you want to remove '{title}' from this customer? Any registered progress for this form will be cleared.",
            "Remove",
            "Cancel");

        if (confirm == ContentDialogResult.Primary)
        {
            await ViewModel.RemoveApplicationCommand.ExecuteAsync(app);
            MainWindow.Instance?.ShowToast("Application removed", InfoBarSeverity.Informational);
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
            if (customer != null && string.IsNullOrWhiteSpace(customer.Mobile))
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

            // Prepare handover request for Billing
            var handover = new BillingHandoverRequest
            {
                CustomerName = customer?.Name ?? "Customer",
                CustomerId = customer?.Id,
                CustomerAddress = customer?.Village,
                SessionId = currentSession.Session.Id,
                Items = []
            };

            if (ViewModel.ActiveApplication != null)
            {
                var app = ViewModel.ActiveApplication;
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

            if (MainWindow.Instance != null)
            {
                MainWindow.Instance.NavigateTo(typeof(PaymentsPage), handover);
            }
        }
    }

    private async void LinkApplication_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedSession?.Customer == null) return;
        var customer = ViewModel.SelectedSession.Customer;
        var dialog = new LinkApplicationDialog(customer.Name, customer.Id, ViewModel.SelectedSession.Session.Id)
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

    private async void StatusDraft_Click(object sender, RoutedEventArgs e) =>
        await ViewModel.UpdateApplicationStatusCommand.ExecuteAsync("Draft");

    private async void StatusDocsReady_Click(object sender, RoutedEventArgs e) =>
        await ViewModel.UpdateApplicationStatusCommand.ExecuteAsync("Docs Ready");

    private async void StatusCompleted_Click(object sender, RoutedEventArgs e) =>
        await ViewModel.UpdateApplicationStatusCommand.ExecuteAsync("Completed");

    private async void ChecklistItem_Checked(object sender, RoutedEventArgs e) =>
        await ViewModel.OnChecklistItemToggledAsync();

    private async void ChecklistItem_Unchecked(object sender, RoutedEventArgs e) =>
        await ViewModel.OnChecklistItemToggledAsync();

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
