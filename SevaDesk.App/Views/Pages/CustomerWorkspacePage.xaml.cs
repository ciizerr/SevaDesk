using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using SevaDesk.Core.Models;
using SevaDesk_App.Services;
using SevaDesk_App.ViewModels.Pages;
using Windows.UI;
using Microsoft.UI;
using System.Linq;
using SevaDesk_App.Views.Dialogs;

namespace SevaDesk_App.Views.Pages;

public sealed partial class CustomerWorkspacePage : Page
{
    public CustomerWorkspaceViewModel ViewModel { get; } = new();

    private FileSystemWatcher? _watcher;
    private DispatcherTimer? _debounceTimer;
    private string? _currentWatchedPath;

    public CustomerWorkspacePage()
    {
        InitializeComponent();
        Unloaded += (s, e) => CleanupWatcher();
        ViewModel.PropertyChanged += (s, args) =>
        {
            if (args.PropertyName == nameof(CustomerWorkspaceViewModel.IsShowingBackup))
            {
                SetupWatcher();
            }
        };
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is Customer customer)
        {
            await ViewModel.InitializeAsync(customer);
            SetupWatcher();
        }
    }

    private void SetupWatcher()
    {
        if (ViewModel.Customer == null) return;

        var folderPath = AppServices.FolderManager.GetEffectiveCustomerFolderPath(
            ViewModel.Customer.Name, ViewModel.Customer.Code, out _);

        if (string.Equals(_currentWatchedPath, folderPath, StringComparison.OrdinalIgnoreCase) && _watcher != null)
            return;

        CleanupWatcher();
        if (!Directory.Exists(folderPath)) return;

        try
        {
            _currentWatchedPath = folderPath;
            _debounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
            _debounceTimer.Tick += (s, e) =>
            {
                _debounceTimer.Stop();
                ViewModel.RefreshFiles();
                ViewModel.NotifyStatsChanged();
            };

            _watcher = new FileSystemWatcher(folderPath)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite | NotifyFilters.Size,
                EnableRaisingEvents = true
            };

            _watcher.Created += OnFolderChanged;
            _watcher.Deleted += OnFolderChanged;
            _watcher.Renamed += OnFolderChanged;
            _watcher.Changed += OnFolderChanged;
        }
        catch { }
    }

    private void OnFolderChanged(object sender, FileSystemEventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            _debounceTimer?.Stop();
            _debounceTimer?.Start();
        });
    }

    private void CleanupWatcher()
    {
        try
        {
            _watcher?.Dispose();
            _watcher = null;
            _debounceTimer?.Stop();
            _debounceTimer = null;
            _currentWatchedPath = null;
        }
        catch { }
    }

    public static Visibility EmptyListVisibility(int count) =>
        count == 0 ? Visibility.Visible : Visibility.Collapsed;

    public static Visibility HasItemsVisibility(int count) =>
        count > 0 ? Visibility.Visible : Visibility.Collapsed;

    public static Visibility VisibleIfHasText(string? text) =>
        !string.IsNullOrWhiteSpace(text) ? Visibility.Visible : Visibility.Collapsed;

    public static string FormatPaymentDate(DateTime dt) => dt.ToString("dd MMM yyyy, hh:mm tt");

    public static string FormatRupee(decimal amount) => $"₹{amount:N0}";

    public static Brush SessionHighlightBackground(bool isHighlighted) =>
        isHighlighted
            ? new SolidColorBrush(Color.FromArgb(35, 59, 130, 246))
            : new SolidColorBrush(Colors.Transparent);

    public static Brush SessionHighlightBorder(bool isHighlighted) =>
        isHighlighted
            ? new SolidColorBrush(Color.FromArgb(255, 59, 130, 246))
            : (Brush)Application.Current.Resources["DividerStrokeColorDefaultBrush"];

    public static Thickness SessionHighlightThickness(bool isHighlighted) =>
        isHighlighted ? new Thickness(2) : new Thickness(0, 0, 0, 1);

    private void PaymentItem_Click(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is Payment payment)
        {
            // Clear prior highlights
            foreach (var s in ViewModel.SessionHistory)
            {
                s.IsHighlighted = false;
            }

            if (!string.IsNullOrWhiteSpace(payment.SessionId))
            {
                var match = ViewModel.SessionHistory.FirstOrDefault(s => s.Session.Id == payment.SessionId);
                if (match != null)
                {
                    match.IsHighlighted = true;
                    SessionHistoryList.SelectedItem = match;
                    SessionHistoryList.ScrollIntoView(match);
                }
                else
                {
                    ViewModel.ShowInfo("Linked session is from an earlier period.");
                }
            }
            else
            {
                ViewModel.ShowInfo("This payment is not linked to a specific session.");
            }
        }
    }

    private async void SessionHistory_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is CustomerSessionRowModel row)
        {
            var dialog = new SessionDetailsDialog(row, ViewModel.Customer)
            {
                XamlRoot = XamlRoot
            };
            await dialog.ShowAsync();
            if (dialog.DeleteRequested)
            {
                await AppServices.Sessions.DeleteSessionAsync(row.Session.Id);
                var s = ViewModel.Sessions.FirstOrDefault(x => x.Id == row.Session.Id);
                if (s != null) ViewModel.Sessions.Remove(s);
                ViewModel.SessionHistory.Remove(row);
                if (ViewModel.Customer != null)
                {
                    AppServices.FolderManager.CleanUpEmptyCustomerWorkingFolder(ViewModel.Customer.Name, ViewModel.Customer.Code);
                    ViewModel.RefreshFiles();
                }
                ViewModel.NotifyStatsChanged();
                MainWindow.Instance?.ShowToast("Session deleted from history", InfoBarSeverity.Success);
            }
            else if (dialog.NavigateToBillingRequested && dialog.HandoverRequest != null)
            {
                MainWindow.Instance?.NavigateTo(typeof(PaymentsPage), dialog.HandoverRequest);
            }
        }
    }

    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        MainWindow.Instance?.GoBack();
    }

    private void OpenFile_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is FolderFileItem item)
        {
            AppServices.FolderManager.OpenFileWithDefaultApp(item.FullPath);
        }
    }

    private void SmartTagFile_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is FolderFileItem item)
        {
            var flyout = SmartTagHelper.CreateTagFlyout(item, async (tag) =>
            {
                await ViewModel.TagAndRenameFileAsync(item, tag, XamlRoot);
            });
            flyout.ShowAt(btn);
        }
    }

    private async void DeleteFile_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is FolderFileItem item)
        {
            var title = AppServices.Localization.GetString("FolderBrowser.DeleteConfirmTitle", "Delete File");
            var msg = $"Are you sure you want to permanently delete '{item.Name}'?";
            var res = await AppServices.Dialogs.ShowConfirmationAsync(title, msg, "Delete", "Cancel");
            if (res == ContentDialogResult.Primary)
            {
                if (AppServices.FolderManager.DeleteFile(item.FullPath, out string err))
                {
                    ViewModel.RefreshFiles();
                }
                else
                {
                    await AppServices.Dialogs.ShowAlertAsync("Delete Failed", err);
                }
            }
        }
    }

    private void AvatarPhoto_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement anchor)
        {
            var flyout = new MenuFlyout();

            var pickItem = new MenuFlyoutItem
            {
                Text = "Choose Photo File...",
                Icon = new FontIcon { Glyph = "\uE8B9" }
            };
            pickItem.Click += async (s, args) =>
            {
                var file = await AppServices.Pickers.PickFileAsync(new[] { ".jpg", ".jpeg", ".png", ".webp", ".bmp" });
                if (!string.IsNullOrWhiteSpace(file) && File.Exists(file))
                {
                    await ViewModel.SetCustomerPhotoAsync(file);
                }
            };
            flyout.Items.Add(pickItem);

            if (!string.IsNullOrWhiteSpace(ViewModel.Customer?.PhotoPath))
            {
                var removeItem = new MenuFlyoutItem
                {
                    Text = "Remove Photo",
                    Icon = new FontIcon { Glyph = "\uE74D", Foreground = new SolidColorBrush(Color.FromArgb(255, 239, 68, 68)) }
                };
                removeItem.Click += async (s, args) =>
                {
                    await ViewModel.RemoveCustomerPhotoAsync();
                };
                flyout.Items.Add(removeItem);
            }

            flyout.ShowAt(anchor);
        }
    }

    private async void EditCustomer_Click(object sender, RoutedEventArgs e)
    {
        var cust = ViewModel.Customer;
        if (cust == null) return;

        var nameBox = new TextBox { Header = "Name", Text = cust.Name };
        var mobileBox = new TextBox { Header = "Mobile", Text = cust.Mobile ?? string.Empty };
        var villageBox = new TextBox { Header = "Village / Area", Text = cust.Village ?? string.Empty };
        var idRefBox = new TextBox { Header = "ID Reference (Aadhaar/PAN)", Text = cust.IdReference ?? string.Empty };
        var notesBox = new TextBox { Header = "Notes / Remarks", Text = cust.Notes ?? string.Empty, AcceptsReturn = true, Height = 60 };

        var dialog = new ContentDialog
        {
            Title = "Edit Customer Details",
            Content = new ScrollViewer
            {
                Content = new StackPanel
                {
                    Spacing = 12,
                    Children = { nameBox, mobileBox, villageBox, idRefBox, notesBox }
                }
            },
            PrimaryButtonText = "Save Changes",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(nameBox.Text))
        {
            cust.Name = nameBox.Text.Trim();
            cust.Mobile = mobileBox.Text.Trim();
            cust.Village = villageBox.Text.Trim();
            cust.IdReference = idRefBox.Text.Trim();
            cust.Notes = notesBox.Text.Trim();

            await AppServices.Customers.UpdateAsync(cust);
            ViewModel.NotifyStatsChanged();
            ViewModel.ShowSuccess("Customer details updated");
        }
    }
}
