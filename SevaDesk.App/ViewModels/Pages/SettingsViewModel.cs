using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Win32;
using SevaDesk_App.Services;
using Windows.ApplicationModel.DataTransfer;
using WinRT.Interop;

namespace SevaDesk_App.ViewModels.Pages;

public partial class SettingsViewModel : ObservableObject
{
    // ==========================================
    // Tab 1: Appearance, Material & Language
    // ==========================================
    [ObservableProperty]
    private int _selectedThemeIndex = 0; // 0 = System, 1 = Light, 2 = Dark

    [ObservableProperty]
    private bool _enableMicaBackdrop = true;

    // Window close behavior: 0 = Always Prompt, 1 = Minimize to Tray, 2 = Exit App
    public static int CloseActionBehavior { get; set; } = 0;

    [ObservableProperty]
    private int _selectedCloseBehaviorIndex;

    public ObservableCollection<LanguageItem> AvailableLanguages => AppServices.Localization.AvailableLanguages;

    [ObservableProperty]
    private LanguageItem? _selectedLanguage;

    [ObservableProperty]
    private bool _isCheckingLanguageUpdates;

    // ==========================================
    // Tab 2: Cyber Café / CSC Business Profile
    // ==========================================
    [ObservableProperty]
    private string _shopName = "SevaDesk Digital Cyber Café";

    [ObservableProperty]
    private string _operatorName = "Ramesh Patel (VLE / Operator)";

    [ObservableProperty]
    private string _cscVleId = "CSC-MH-2024-9842";

    [ObservableProperty]
    private string _contactNumber = "+91 98765 43210";

    [ObservableProperty]
    private string _shopAddress = "Shop #4, Panchayat Complex, Main Market";

    [ObservableProperty]
    private string _shopUpiVpa = "sevadesk.csc@upi";

    [ObservableProperty]
    private string _payeeName = "SevaDesk Cyber Center";

    // ==========================================
    // Tab 3: Hardware & Pricing Rates
    // ==========================================
    public ObservableCollection<string> InstalledPrinters { get; } = new();

    [ObservableProperty]
    private string _defaultBwPrinter = "Brother DCP-L2520D series";

    [ObservableProperty]
    private string _defaultColorPrinter = "Epson EcoTank L8050 Photo";

    [ObservableProperty]
    private decimal _bwSinglePageRate = 5;

    [ObservableProperty]
    private decimal _bwDoublePageRate = 8;

    [ObservableProperty]
    private decimal _colorPageRate = 15;

    [ObservableProperty]
    private decimal _photoPrintRate = 30;

    [ObservableProperty]
    private decimal _laminationRate = 20;

    [ObservableProperty]
    private decimal _scanRate = 10;

    // ==========================================
    // Tab 4: Storage & Automation
    // ==========================================
    [ObservableProperty]
    private string _workingRootPath = string.Empty;

    [ObservableProperty]
    private bool _watchDownloads = true;

    [ObservableProperty]
    private bool _watchDesktop = true;

    [ObservableProperty]
    private bool _watchDocuments = true;

    public ObservableCollection<string> CustomWatchFolders => AppServices.FileWatcher.CustomFolders;

    [ObservableProperty]
    private int _autoArchiveDaysIndex = 2; // 0: 7 days, 1: 15 days, 2: 30 days, 3: 60 days, 4: Never

    [ObservableProperty]
    private string _databasePath = string.Empty;

    // ==========================================
    // Tab 5: About & System (Minimal)
    // ==========================================
    [ObservableProperty]
    private string _appVersionDisplay = "SevaDesk v1.0.0-preview";

    [ObservableProperty]
    private string _buildInfoDisplay = "Build 2026.09.14 · WinUI 3 (Windows App SDK 2.4) · .NET 8.0 (win-x64)";

    [ObservableProperty]
    private string _developerName = "ciizerr";

    [ObservableProperty]
    private string _githubUrl = "https://github.com/ciizerr";

    [ObservableProperty]
    private string _bugReportUrl = "https://github.com/ciizerr/SevaDesk/issues/new?template=bug_report.md";

    [ObservableProperty]
    private string _featureRequestUrl = "https://github.com/ciizerr/SevaDesk/issues/new?template=feature_request.md";

    [ObservableProperty]
    private string _communityUrl = "https://github.com/ciizerr/SevaDesk/discussions";

    [ObservableProperty]
    private string _buyMeCoffeeUrl = "https://buymeacoffee.com/ciizerr";

    [ObservableProperty]
    private string _upiId = "sevadesk.csc@upi";

    // ==========================================
    // Feedback & Status (with Auto-Dismiss Timer)
    // ==========================================
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStatusMessage))]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private InfoBarSeverity _statusSeverity = InfoBarSeverity.Success;

    private CancellationTokenSource? _statusDismissCts;

    public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);

    public SettingsViewModel()
    {
        WorkingRootPath = AppServices.FolderManager.BaseDirectory;
        WatchDownloads = AppServices.FileWatcher.WatchDownloads;
        WatchDesktop = AppServices.FileWatcher.WatchDesktop;
        WatchDocuments = AppServices.FileWatcher.WatchDocuments;
        
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        DatabasePath = Path.Combine(appData, "SevaDesk", "sevadesk.db");

        LoadInstalledPrinters();

        // Initialize active theme and mica state from MainWindow
        if (MainWindow.Instance != null)
        {
            _enableMicaBackdrop = MainWindow.Instance.IsMicaEnabled;
            _selectedThemeIndex = MainWindow.Instance.CurrentTheme switch
            {
                ElementTheme.Light => 1,
                ElementTheme.Dark => 2,
                _ => 0
            };
        }

        _selectedCloseBehaviorIndex = CloseActionBehavior;

        // Initialize active language selection
        _selectedLanguage = AvailableLanguages.FirstOrDefault(l => string.Equals(l.Code, AppServices.Localization.CurrentLanguageCode, StringComparison.OrdinalIgnoreCase))
                           ?? AvailableLanguages.FirstOrDefault();

        AppServices.Localization.LanguageChanged += (s, e) =>
        {
            OnPropertyChanged(nameof(AvailableLanguages));
            OnPropertyChanged(nameof(SelectedLanguage));
        };
    }

    private void LoadInstalledPrinters()
    {
        InstalledPrinters.Clear();
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows NT\CurrentVersion\Devices");
            if (key != null)
            {
                foreach (var printer in key.GetValueNames())
                {
                    if (!string.IsNullOrWhiteSpace(printer))
                    {
                        InstalledPrinters.Add(printer);
                    }
                }
            }
        }
        catch
        {
            // fallback
        }

        if (InstalledPrinters.Count == 0)
        {
            InstalledPrinters.Add("Microsoft Print to PDF");
            InstalledPrinters.Add("OneNote (Desktop)");
        }

        // Set default printer selection if not in list
        if (!InstalledPrinters.Contains(DefaultBwPrinter) && InstalledPrinters.Count > 0)
        {
            DefaultBwPrinter = InstalledPrinters[0];
        }
        if (!InstalledPrinters.Contains(DefaultColorPrinter) && InstalledPrinters.Count > 0)
        {
            DefaultColorPrinter = InstalledPrinters.Count > 1 ? InstalledPrinters[1] : InstalledPrinters[0];
        }
    }

    partial void OnSelectedThemeIndexChanged(int value)
    {
        var theme = value switch
        {
            1 => ElementTheme.Light,
            2 => ElementTheme.Dark,
            _ => ElementTheme.Default
        };

        MainWindow.Instance?.SetTheme(theme);
        AutoSave("Theme applied.");
    }

    partial void OnEnableMicaBackdropChanged(bool value)
    {
        MainWindow.Instance?.SetMicaBackdrop(value);
        AutoSave("Backdrop updated.");
    }

    partial void OnSelectedCloseBehaviorIndexChanged(int value)
    {
        CloseActionBehavior = value;
        AutoSave("Close behavior updated.");
    }

    partial void OnWatchDownloadsChanged(bool value)
    {
        AppServices.FileWatcher.WatchDownloads = value;
        AppServices.FileWatcher.SaveSettings();
        AppServices.FileWatcher.RestartWatchers();
        AutoSave("Watched folders updated.");
    }

    partial void OnWatchDesktopChanged(bool value)
    {
        AppServices.FileWatcher.WatchDesktop = value;
        AppServices.FileWatcher.SaveSettings();
        AppServices.FileWatcher.RestartWatchers();
        AutoSave("Watched folders updated.");
    }

    partial void OnWatchDocumentsChanged(bool value)
    {
        AppServices.FileWatcher.WatchDocuments = value;
        AppServices.FileWatcher.SaveSettings();
        AppServices.FileWatcher.RestartWatchers();
        AutoSave("Watched folders updated.");
    }

    partial void OnSelectedLanguageChanged(LanguageItem? value)
    {
        if (value != null && !string.Equals(value.Code, AppServices.Localization.CurrentLanguageCode, StringComparison.OrdinalIgnoreCase))
        {
            AppServices.Localization.SetLanguage(value.Code);
            AutoSave($"Language changed to {value.DisplayName}.");
        }
    }

    public string Text(string key) => AppServices.Localization.GetString(key);

    public void ShowStatus(string message, InfoBarSeverity severity = InfoBarSeverity.Success, int durationMs = 4000)
    {
        _statusDismissCts?.Cancel();
        _statusDismissCts?.Dispose();
        _statusDismissCts = new CancellationTokenSource();
        var token = _statusDismissCts.Token;

        StatusSeverity = severity;
        StatusMessage = message;

        if (durationMs > 0)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(durationMs, token);
                    if (!token.IsCancellationRequested)
                    {
                        MainWindow.Instance?.DispatcherQueue.TryEnqueue(() =>
                        {
                            if (!token.IsCancellationRequested)
                            {
                                StatusMessage = string.Empty;
                            }
                        });
                    }
                }
                catch (OperationCanceledException) { }
            });
        }
    }

    public void AutoSave(string? message = null)
    {
        ShowStatus(message ?? "Changes saved automatically.", InfoBarSeverity.Success, 4000);
    }

    [RelayCommand]
    public async Task CheckLanguageUpdatesAsync()
    {
        IsCheckingLanguageUpdates = true;
        ShowStatus("Checking GitHub for language updates...", InfoBarSeverity.Informational, 0);
        try
        {
            var (success, msg, count) = await AppServices.Localization.SyncFromRemoteAsync(true);
            var severity = success ? InfoBarSeverity.Success : InfoBarSeverity.Warning;
            ShowStatus(msg, severity, 4000);
            if (success)
            {
                OnPropertyChanged(nameof(AvailableLanguages));
                SelectedLanguage = AvailableLanguages.FirstOrDefault(l => string.Equals(l.Code, AppServices.Localization.CurrentLanguageCode, StringComparison.OrdinalIgnoreCase));
            }
        }
        finally
        {
            IsCheckingLanguageUpdates = false;
        }
    }

    [RelayCommand]
    public void OpenGitHub() => OpenUrl(GithubUrl);

    [RelayCommand]
    public void ReportBug() => OpenUrl(BugReportUrl);

    [RelayCommand]
    public void RequestFeature() => OpenUrl(FeatureRequestUrl);

    [RelayCommand]
    public void OpenCommunity() => OpenUrl(CommunityUrl);

    [RelayCommand]
    public void OpenBuyMeCoffee() => OpenUrl(BuyMeCoffeeUrl);

    [RelayCommand]
    public void CopyUpi()
    {
        try
        {
            var dataPackage = new DataPackage();
            dataPackage.SetText(UpiId);
            Clipboard.SetContent(dataPackage);
            ShowStatus($"UPI ID copied to clipboard: {UpiId}", InfoBarSeverity.Success, 4000);
        }
        catch (Exception ex)
        {
            ShowStatus($"Could not copy UPI ID: {ex.Message}", InfoBarSeverity.Warning, 4000);
        }
    }

    private void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            ShowStatus($"Could not open link: {ex.Message}", InfoBarSeverity.Warning, 4000);
        }
    }

    [RelayCommand]
    public void OpenWorkingFolder()
    {
        AppServices.FolderManager.OpenFolderInExplorer(WorkingRootPath);
    }

    [RelayCommand]
    public async Task BrowseWorkingFolderAsync()
    {
        try
        {
            var picker = new Windows.Storage.Pickers.FolderPicker();
            if (MainWindow.Instance != null)
            {
                var hwnd = Win32Interop.GetWindowFromWindowId(MainWindow.Instance.AppWindow.Id);
                InitializeWithWindow.Initialize(picker, hwnd);
            }

            picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.Desktop;
            picker.FileTypeFilter.Add("*");

            var folder = await picker.PickSingleFolderAsync();
            if (folder != null && !string.IsNullOrWhiteSpace(folder.Path))
            {
                WorkingRootPath = folder.Path;
                AppServices.FolderManager.SetBaseDirectory(folder.Path);
                AppServices.Database.SetSetting("working_root_path", folder.Path);
                AppServices.FileWatcher.RestartWatchers();
                AutoSave("Working folder changed successfully.");
            }
        }
        catch (Exception ex)
        {
            ShowStatus($"Error changing folder: {ex.Message}", InfoBarSeverity.Error, 5000);
        }
    }

    [RelayCommand]
    public async Task AddCustomWatchFolderAsync()
    {
        try
        {
            var picker = new Windows.Storage.Pickers.FolderPicker();
            if (MainWindow.Instance != null)
            {
                var hwnd = Win32Interop.GetWindowFromWindowId(MainWindow.Instance.AppWindow.Id);
                InitializeWithWindow.Initialize(picker, hwnd);
            }

            picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.ComputerFolder;
            picker.FileTypeFilter.Add("*");

            var folder = await picker.PickSingleFolderAsync();
            if (folder != null && !string.IsNullOrWhiteSpace(folder.Path))
            {
                if (!CustomWatchFolders.Contains(folder.Path))
                {
                    CustomWatchFolders.Add(folder.Path);
                    AppServices.FileWatcher.SaveSettings();
                    AppServices.FileWatcher.RestartWatchers();
                    AutoSave($"Added watched folder: {folder.Name}");
                }
            }
        }
        catch (Exception ex)
        {
            ShowStatus($"Error adding folder: {ex.Message}", InfoBarSeverity.Error, 5000);
        }
    }

    [RelayCommand]
    public void RemoveCustomWatchFolder(string path)
    {
        if (CustomWatchFolders.Remove(path))
        {
            AppServices.FileWatcher.SaveSettings();
            AppServices.FileWatcher.RestartWatchers();
            AutoSave("Custom watched folder removed.");
        }
    }

    [RelayCommand]
    public void BackupDatabase()
    {
        try
        {
            if (File.Exists(DatabasePath))
            {
                var dir = Path.GetDirectoryName(DatabasePath) ?? string.Empty;
                var backupDir = Path.Combine(dir, "Backups");
                Directory.CreateDirectory(backupDir);
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var backupPath = Path.Combine(backupDir, $"sevadesk_backup_{timestamp}.db");
                File.Copy(DatabasePath, backupPath, true);
                ShowStatus($"Backup created successfully: {Path.GetFileName(backupPath)}", InfoBarSeverity.Success, 4000);
            }
            else
            {
                ShowStatus("Database file not found to backup.", InfoBarSeverity.Warning, 4000);
            }
        }
        catch (Exception ex)
        {
            ShowStatus($"Backup failed: {ex.Message}", InfoBarSeverity.Error, 5000);
        }
    }

    [RelayCommand]
    public void SaveSettings()
    {
        ShowStatus("All settings saved successfully.", InfoBarSeverity.Success, 4000);
    }

    [RelayCommand]
    public void ResetDefaults()
    {
        SelectedThemeIndex = 0;
        EnableMicaBackdrop = true;
        CloseActionBehavior = 0;
        SelectedCloseBehaviorIndex = 0;

        ShopName = "SevaDesk Digital Cyber Café";
        OperatorName = "Ramesh Patel (VLE / Operator)";
        CscVleId = "CSC-MH-2024-9842";
        ContactNumber = "+91 98765 43210";
        ShopAddress = "Shop #4, Panchayat Complex, Main Market";
        ShopUpiVpa = "sevadesk.csc@upi";
        PayeeName = "SevaDesk Cyber Center";

        BwSinglePageRate = 5;
        BwDoublePageRate = 8;
        ColorPageRate = 15;
        PhotoPrintRate = 30;
        LaminationRate = 20;
        ScanRate = 10;

        WatchDownloads = true;
        WatchDesktop = true;
        WatchDocuments = true;
        AutoArchiveDaysIndex = 2;

        StatusMessage = "Defaults restored successfully.";
    }
}
