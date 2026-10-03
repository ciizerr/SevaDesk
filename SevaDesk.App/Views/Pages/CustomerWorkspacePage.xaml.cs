using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Input;
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

    private readonly Action<string> _avatarUpdatedHandler;

    public CustomerWorkspacePage()
    {
        InitializeComponent();
        _avatarUpdatedHandler = (photo) =>
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                if (ViewModel.Customer != null)
                {
                    ViewModel.Customer.NotifyPhotoUpdated();
                }
            });
        };
        CustomerAvatarHelper.AvatarUpdated += _avatarUpdatedHandler;
        Unloaded += (s, e) =>
        {
            CustomerAvatarHelper.AvatarUpdated -= _avatarUpdatedHandler;
            CleanupWatcher();
        };
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
            CheckCustomerPhoto();
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
                CheckCustomerPhoto();
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

    private void CheckCustomerPhoto()
    {
        if (ViewModel.Customer == null) return;
        try
        {
            var detectedPhoto = CustomerAvatarHelper.FindPhotoInCustomerFolders(ViewModel.Customer.Name, ViewModel.Customer.Code);
            if (!string.IsNullOrWhiteSpace(detectedPhoto))
            {
                if (!string.Equals(ViewModel.Customer.PhotoPath, detectedPhoto, StringComparison.OrdinalIgnoreCase))
                {
                    ViewModel.Customer.PhotoPath = detectedPhoto;
                    _ = AppServices.Customers.UpdatePhotoAsync(ViewModel.Customer.Id, detectedPhoto);
                    CustomerAvatarHelper.NotifyAvatarUpdated(detectedPhoto);
                }
                else
                {
                    ViewModel.Customer.NotifyPhotoUpdated();
                }
            }
        }
        catch { }
    }

    public static Visibility EmptyListVisibility(int count) =>
        count == 0 ? Visibility.Visible : Visibility.Collapsed;

    public static Visibility HasItemsVisibility(int count) =>
        count > 0 ? Visibility.Visible : Visibility.Collapsed;

    public static Visibility VisibleIfHasText(string? text) =>
        !string.IsNullOrWhiteSpace(text) ? Visibility.Visible : Visibility.Collapsed;

    public static Visibility VisibleIfCanWhatsApp(string? mobile)
    {
        if (string.IsNullOrWhiteSpace(mobile)) return Visibility.Collapsed;
        var digits = mobile.Count(char.IsDigit);
        return digits >= 10 ? Visibility.Visible : Visibility.Collapsed;
    }

    public static Visibility TabVisibility(int selected, int target) =>
        selected == target ? Visibility.Visible : Visibility.Collapsed;

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

    public static Brush ItemBadgeBackground(bool isDiscount) =>
        isDiscount
            ? new SolidColorBrush(Color.FromArgb(30, 16, 185, 129))
            : (Brush)Application.Current.Resources["SubtleFillColorSecondaryBrush"];

    public static Brush ItemGlyphForeground(bool isDiscount) =>
        isDiscount
            ? new SolidColorBrush(Color.FromArgb(255, 16, 185, 129))
            : (Brush)Application.Current.Resources["AccentTextFillColorPrimaryBrush"];

    public static Brush ItemTextForeground(bool isDiscount) =>
        isDiscount
            ? new SolidColorBrush(Color.FromArgb(255, 16, 185, 129))
            : (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"];

    public static Brush TabSegmentBackground(int selected, int target) =>
        selected == target
            ? (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"]
            : new SolidColorBrush(Colors.Transparent);

    public static Brush TabSegmentBorder(int selected, int target) =>
        selected == target
            ? (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"]
            : new SolidColorBrush(Colors.Transparent);

    public static Brush TabSegmentForeground(int selected, int target) =>
        selected == target
            ? (Brush)Application.Current.Resources["AccentTextFillColorPrimaryBrush"]
            : (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"];

    public static Windows.UI.Text.FontWeight TabSegmentFontWeight(int selected, int target) =>
        selected == target
            ? Microsoft.UI.Text.FontWeights.SemiBold
            : Microsoft.UI.Text.FontWeights.Normal;

    private void ActivityTabBtn_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string tagStr && int.TryParse(tagStr, out int tabIdx))
        {
            ViewModel.SelectedActivityTab = tabIdx;
        }
    }

    private void ActivityTab_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton rb && rb.Tag is string tagStr && int.TryParse(tagStr, out int tabIdx))
        {
            ViewModel.SelectedActivityTab = tabIdx;
        }
    }

    private async void PaymentItem_Click(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is Payment payment)
        {
            Session? session = null;
            if (!string.IsNullOrWhiteSpace(payment.SessionId))
            {
                session = ViewModel.Sessions.FirstOrDefault(s => s.Id == payment.SessionId);
            }

            var receiptInfo = CompletedReceiptInfo.FromPayment(payment, ViewModel.Customer, session);
            var dialog = new ReceiptDialog(receiptInfo)
            {
                XamlRoot = XamlRoot
            };
            await dialog.ShowAsync();
        }
    }

    private async void SessionHistory_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is CustomerSessionRowModel row)
        {
            if (row.HasPayment && row.Payment != null)
            {
                var receiptInfo = CompletedReceiptInfo.FromPayment(row.Payment, ViewModel.Customer, row.Session);
                var dialog = new ReceiptDialog(receiptInfo)
                {
                    XamlRoot = XamlRoot
                };
                await dialog.ShowAsync();
            }
            else
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
    }

    private async void TimelineItem_Click(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is CustomerTimelineItemModel item)
        {
            if (item.IsPayment && item.Payment != null)
            {
                Session? session = null;
                if (!string.IsNullOrWhiteSpace(item.Payment.SessionId))
                {
                    session = ViewModel.Sessions.FirstOrDefault(s => s.Id == item.Payment.SessionId);
                }

                var receiptInfo = CompletedReceiptInfo.FromPayment(item.Payment, ViewModel.Customer, session);
                var dialog = new ReceiptDialog(receiptInfo)
                {
                    XamlRoot = XamlRoot
                };
                dialog.PaymentSettled += async () =>
                {
                    await ViewModel.RefreshAsync();
                };
                await dialog.ShowAsync();
            }
            else if (item.SessionRow != null)
            {
                if (item.SessionRow.HasPayment && item.SessionRow.Payment != null)
                {
                    var receiptInfo = CompletedReceiptInfo.FromPayment(item.SessionRow.Payment, ViewModel.Customer, item.SessionRow.Session);
                    var dialog = new ReceiptDialog(receiptInfo)
                    {
                        XamlRoot = XamlRoot
                    };
                    await dialog.ShowAsync();
                }
                else
                {
                    var dialog = new SessionDetailsDialog(item.SessionRow, ViewModel.Customer)
                    {
                        XamlRoot = XamlRoot
                    };
                    await dialog.ShowAsync();
                    if (dialog.DeleteRequested)
                    {
                        await AppServices.Sessions.DeleteSessionAsync(item.SessionRow.Session.Id);
                        var s = ViewModel.Sessions.FirstOrDefault(x => x.Id == item.SessionRow.Session.Id);
                        if (s != null) ViewModel.Sessions.Remove(s);
                        ViewModel.SessionHistory.Remove(item.SessionRow);
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
        }
    }

    private async void StartSession_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.Customer == null) return;
        try
        {
            await ViewModel.StartSessionAsync();
            MainWindow.Instance?.ShowToast($"Active session started for {ViewModel.Customer.Name}", InfoBarSeverity.Success);
            MainWindow.Instance?.NavigateTo(typeof(SessionsPage));
        }
        catch (Exception ex)
        {
            ViewModel.ShowError($"Failed to start session: {ex.Message}");
        }
    }

    private void NewBill_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.Customer == null) return;
        var handover = new BillingHandoverRequest
        {
            CustomerId = ViewModel.Customer.Id,
            CustomerName = ViewModel.Customer.Name,
            CustomerAddress = ViewModel.Customer.Village
        };
        MainWindow.Instance?.NavigateTo(typeof(PaymentsPage), handover);
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.OpenCustomerFolder();
    }

    private async void ImportFiles_Click(object sender, RoutedEventArgs e)
    {
        var count = await ViewModel.ImportFilesAsync();
        if (count > 0)
        {
            MainWindow.Instance?.ShowToast($"Added {count} file(s) to customer folder", InfoBarSeverity.Success);
        }
    }

    private void CopyPhone_Click(object sender, RoutedEventArgs e)
    {
        var mobile = ViewModel.Customer?.Mobile?.Trim();
        if (!string.IsNullOrWhiteSpace(mobile))
        {
            var dp = new Windows.ApplicationModel.DataTransfer.DataPackage();
            dp.SetText(mobile);
            Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dp);
            MainWindow.Instance?.ShowToast($"Copied {mobile} to clipboard.", InfoBarSeverity.Informational);
        }
    }

    private async void WhatsApp_Click(object sender, RoutedEventArgs e)
    {
        var mobile = ViewModel.Customer?.Mobile;
        var digits = new string(mobile?.Where(char.IsDigit).ToArray() ?? Array.Empty<char>());
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

    private void CustomerFileRow_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        FolderFileItem? item = null;
        if (sender is FrameworkElement fe)
        {
            item = fe.Tag as FolderFileItem ?? fe.DataContext as FolderFileItem;
        }

        if (item != null && !string.IsNullOrWhiteSpace(item.FullPath) && System.IO.File.Exists(item.FullPath))
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

    private static TextBlock CreateRequiredHeader(string label)
    {
        var tb = new TextBlock { FontSize = 12 };
        tb.Inlines.Add(new Run { Text = label + " " });
        tb.Inlines.Add(new Run { Text = "*", Foreground = new SolidColorBrush(Color.FromArgb(255, 239, 68, 68)), FontWeight = Microsoft.UI.Text.FontWeights.Bold });
        return tb;
    }

    private async void EditCustomer_Click(object sender, RoutedEventArgs e)
    {
        var cust = ViewModel.Customer;
        if (cust == null) return;

        var nameBox = new TextBox
        {
            Header = CreateRequiredHeader("Customer Name"),
            Text = cust.Name,
            PlaceholderText = "Full name"
        };
        var mobileBox = new TextBox
        {
            Header = "Mobile Number",
            Text = cust.Mobile ?? string.Empty,
            PlaceholderText = "10-digit number",
            MaxLength = 10
        };
        var mobileError = new TextBlock
        {
            Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SystemFillColorCriticalBrush"],
            FontSize = 11,
            Margin = new Thickness(2, 0, 0, 0),
            Visibility = Visibility.Collapsed
        };
        var mobilePanel = new StackPanel
        {
            Spacing = 4,
            Children = { mobileBox, mobileError }
        };

        var villageBox = new TextBox
        {
            Header = "Village / City",
            Text = cust.Village ?? string.Empty,
            PlaceholderText = "Village, town, or area"
        };
        var idRefBox = new TextBox
        {
            Header = "ID Reference / Aadhaar",
            Text = cust.IdReference ?? string.Empty,
            PlaceholderText = "Aadhaar or ID ref"
        };
        var notesBox = new TextBox
        {
            Header = "Notes",
            Text = cust.Notes ?? string.Empty,
            PlaceholderText = "Special notes or remarks",
            AcceptsReturn = true,
            Height = 60
        };

        var dialog = new ContentDialog
        {
            Title = "Edit Customer",
            Content = new ScrollViewer
            {
                Content = new StackPanel
                {
                    Spacing = 12,
                    Children = { nameBox, mobilePanel, villageBox, idRefBox, notesBox }
                }
            },
            PrimaryButtonText = "Save",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot
        };

        void ValidateInputs()
        {
            var mob = mobileBox.Text?.Trim() ?? string.Empty;
            if (mob.Length > 0 && mob.Length < 10)
            {
                mobileError.Text = $"Please enter 10 digits ({mob.Length}/10 entered)";
                mobileError.Visibility = Visibility.Visible;
                dialog.IsPrimaryButtonEnabled = false;
            }
            else
            {
                mobileError.Visibility = Visibility.Collapsed;
                dialog.IsPrimaryButtonEnabled = !string.IsNullOrWhiteSpace(nameBox.Text);
            }
        }

        mobileBox.ConfigureNumericMobileInput(_ => ValidateInputs());
        nameBox.TextChanged += (s, e) => ValidateInputs();
        ValidateInputs();

        dialog.Closing += (sender, args) =>
        {
            if (args.Result == ContentDialogResult.Primary)
            {
                var mob = mobileBox.Text?.Trim() ?? string.Empty;
                if (mob.Length > 0 && mob.Length < 10)
                {
                    args.Cancel = true;
                    ValidateInputs();
                    mobileBox.Focus(FocusState.Programmatic);
                }
            }
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

    private async void DeleteCustomer_Click(object sender, RoutedEventArgs e)
    {
        var customer = ViewModel.Customer;
        if (customer == null) return;

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

            // 5. Toast feedback
            if (action == DeleteCustomerAction.DeleteAndWipeAll)
            {
                MainWindow.Instance?.ShowToast($"Customer '{customer.Name}' and all files permanently deleted.", InfoBarSeverity.Success);
            }
            else
            {
                MainWindow.Instance?.ShowToast($"Customer '{customer.Name}' deleted. Backup files preserved.", InfoBarSeverity.Success);
            }

            // 6. Navigate back
            MainWindow.Instance?.GoBack();
        }
        catch (Exception ex)
        {
            ViewModel.ShowError($"Error deleting customer: {ex.Message}");
        }
    }
}
