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

public partial class SettingsViewModel : StatusViewModel
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
    // Overlay Widget Settings
    // ==========================================
    public static bool IsOverlayWidgetEnabledSetting
    {
        get
        {
            var val = AppServices.Database.GetSetting("overlay_widget_enabled", "true");
            return bool.TryParse(val, out var b) ? b : true;
        }
        set => AppServices.Database.SetSetting("overlay_widget_enabled", value.ToString().ToLowerInvariant());
    }

    public static int OverlayPositionSetting
    {
        get
        {
            var val = AppServices.Database.GetSetting("overlay_position", "0");
            return int.TryParse(val, out var idx) ? idx : 0;
        }
        set => AppServices.Database.SetSetting("overlay_position", value.ToString());
    }

    [ObservableProperty]
    private bool _isOverlayWidgetEnabled = true;

    [ObservableProperty]
    private int _overlayPositionIndex = 0; // 0: Bottom-Right, 1: Top-Right, 2: Bottom-Left, 3: Top-Left

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

        // Initialize Overlay Widget Settings
        _isOverlayWidgetEnabled = IsOverlayWidgetEnabledSetting;
        _overlayPositionIndex = OverlayPositionSetting;

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

    partial void OnIsOverlayWidgetEnabledChanged(bool value)
    {
        IsOverlayWidgetEnabledSetting = value;
        AutoSave("Overlay widget setting updated.");
    }

    partial void OnOverlayPositionIndexChanged(int value)
    {
        OverlayPositionSetting = value;
        AutoSave("Overlay position updated.");
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


    public void AutoSave(string? message = null)
    {
        ShowSuccess(message ?? "Changes saved automatically.");
    }

    [RelayCommand]
    public async Task CheckLanguageUpdatesAsync()
    {
        IsCheckingLanguageUpdates = true;
        ShowInfo("Checking GitHub for language updates...");
        try
        {
            var (success, msg, count) = await AppServices.Localization.SyncFromRemoteAsync(true);
            if (success) ShowSuccess(msg); else ShowWarning(msg);
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
            ShowSuccess($"UPI ID copied to clipboard: {UpiId}");
        }
        catch (Exception ex)
        {
            ShowWarning($"Could not copy UPI ID: {ex.Message}");
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
            ShowWarning($"Could not open link: {ex.Message}");
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
            var folderPath = await AppServices.Pickers.PickFolderAsync();
            if (folderPath != null && !string.IsNullOrWhiteSpace(folderPath))
            {
                WorkingRootPath = folderPath;
                AppServices.FolderManager.SetBaseDirectory(folderPath);
                AppServices.Database.SetSetting("working_root_path", folderPath);
                AppServices.FileWatcher.RestartWatchers();
                AutoSave("Working folder changed successfully.");
            }
        }
        catch (Exception ex)
        {
            ShowError($"Error changing folder: {ex.Message}");
        }
    }

    [RelayCommand]
    public async Task AddCustomWatchFolderAsync()
    {
        try
        {
            var folderPath = await AppServices.Pickers.PickFolderAsync();
            if (folderPath != null && !string.IsNullOrWhiteSpace(folderPath))
            {
                if (!CustomWatchFolders.Contains(folderPath))
                {
                    CustomWatchFolders.Add(folderPath);
                    AppServices.FileWatcher.SaveSettings();
                    AppServices.FileWatcher.RestartWatchers();
                    AutoSave($"Added watched folder: {Path.GetFileName(folderPath)}");
                }
            }
        }
        catch (Exception ex)
        {
            ShowError($"Error adding folder: {ex.Message}");
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
                ShowSuccess($"Backup created successfully: {Path.GetFileName(backupPath)}");
            }
            else
            {
                ShowWarning("Database file not found to backup.");
            }
        }
        catch (Exception ex)
        {
            ShowError($"Backup failed: {ex.Message}");
        }
    }

    [RelayCommand]
    public void SaveSettings()
    {
        ShowSuccess("All settings saved successfully.");
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

        ShowSuccess("Defaults restored successfully.");
    }
}
