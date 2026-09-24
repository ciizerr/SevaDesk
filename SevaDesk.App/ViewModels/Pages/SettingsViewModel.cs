using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Win32;
using SevaDesk_App.Services;
using WinRT.Interop;

namespace SevaDesk_App.ViewModels.Pages;

public partial class SettingsViewModel : StatusViewModel
{
    // ==========================================
    // Dirty / Save State
    // ==========================================
    [ObservableProperty]
    private bool _hasUnsavedChanges = false;

    // ==========================================
    // Tab 1: Appearance, Material & Language
    // ==========================================
    [ObservableProperty]
    private int _selectedThemeIndex = 0; // 0 = System, 1 = Light, 2 = Dark

    [ObservableProperty]
    private bool _enableMicaBackdrop = true;

    // Window close behavior: 0 = Always Prompt, 1 = Minimize to Tray, 2 = Exit App
    public static int CloseActionBehavior
    {
        get
        {
            var val = AppServices.Database.GetSetting("close_behavior", "0");
            return int.TryParse(val, out var b) ? b : 0;
        }
        set => AppServices.Database.SetSetting("close_behavior", value.ToString());
    }

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



    // ==========================================
    // Tab 4: Storage & Automation
    // ==========================================
    [ObservableProperty]
    private string _workingRootPath = string.Empty;

    [ObservableProperty]
    private string _backupRootPath = string.Empty;

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

    public SettingsViewModel()
    {
        // Load café business profile settings from DB (use backing fields to avoid premature dirty flag)
        _shopName = AppServices.Database.GetSetting("shop_name", _shopName) ?? _shopName;
        _operatorName = AppServices.Database.GetSetting("operator_name", _operatorName) ?? _operatorName;
        _cscVleId = AppServices.Database.GetSetting("csc_vle_id", _cscVleId) ?? _cscVleId;
        _contactNumber = AppServices.Database.GetSetting("contact_number", _contactNumber) ?? _contactNumber;
        _shopAddress = AppServices.Database.GetSetting("shop_address", _shopAddress) ?? _shopAddress;
        _shopUpiVpa = AppServices.Database.GetSetting("shop_upi_vpa")
                      ?? AppServices.Database.GetSetting("shop_upi_id", _shopUpiVpa)
                      ?? _shopUpiVpa;
        _payeeName = AppServices.Database.GetSetting("payee_name", _payeeName) ?? _payeeName;

        // Load printer preferences from DB
        _defaultBwPrinter = AppServices.Database.GetSetting("default_bw_printer", _defaultBwPrinter) ?? _defaultBwPrinter;
        _defaultColorPrinter = AppServices.Database.GetSetting("default_color_printer", _defaultColorPrinter) ?? _defaultColorPrinter;

        _workingRootPath = AppServices.FolderManager.BaseDirectory;
        _backupRootPath = AppServices.FolderManager.BackupDirectory;

        // BUG FIX: Use backing-field assignment so OnWatchXxxChanged partial handlers
        // do NOT fire during construction (which was triggering the "Watched folders updated." toast).
        _watchDownloads = AppServices.FileWatcher.WatchDownloads;
        _watchDesktop = AppServices.FileWatcher.WatchDesktop;
        _watchDocuments = AppServices.FileWatcher.WatchDocuments;

        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _databasePath = Path.Combine(appData, "SevaDesk", "sevadesk.db");

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

        // Use backing fields so OnXxxChanged handlers don't fire on init
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

    // ==========================================
    // Live-Apply Settings (no dirty flag)
    // ==========================================

    partial void OnSelectedThemeIndexChanged(int value)
    {
        var theme = value switch
        {
            1 => ElementTheme.Light,
            2 => ElementTheme.Dark,
            _ => ElementTheme.Default
        };
        MainWindow.Instance?.SetTheme(theme);
        ShowSuccess("Theme applied.");
    }

    partial void OnEnableMicaBackdropChanged(bool value)
    {
        MainWindow.Instance?.SetMicaBackdrop(value);
        ShowSuccess("Backdrop updated.");
    }

    partial void OnSelectedLanguageChanged(LanguageItem? value)
    {
        if (value != null && !string.Equals(value.Code, AppServices.Localization.CurrentLanguageCode, StringComparison.OrdinalIgnoreCase))
        {
            AppServices.Localization.SetLanguage(value.Code);
            ShowSuccess($"Language changed to {value.DisplayName}.");
        }
    }

    // ==========================================
    // Dirty-Flag Settings (require explicit Save)
    // ==========================================

    partial void OnSelectedCloseBehaviorIndexChanged(int value)
    {
        CloseActionBehavior = value;
        HasUnsavedChanges = true;
    }

    partial void OnShopNameChanged(string value) => HasUnsavedChanges = true;
    partial void OnOperatorNameChanged(string value) => HasUnsavedChanges = true;
    partial void OnCscVleIdChanged(string value) => HasUnsavedChanges = true;
    partial void OnContactNumberChanged(string value) => HasUnsavedChanges = true;
    partial void OnShopAddressChanged(string value) => HasUnsavedChanges = true;
    partial void OnShopUpiVpaChanged(string value) => HasUnsavedChanges = true;
    partial void OnPayeeNameChanged(string value) => HasUnsavedChanges = true;
    partial void OnDefaultBwPrinterChanged(string value) => HasUnsavedChanges = true;
    partial void OnDefaultColorPrinterChanged(string value) => HasUnsavedChanges = true;

    partial void OnWatchDownloadsChanged(bool value)
    {
        AppServices.FileWatcher.WatchDownloads = value;
        AppServices.FileWatcher.SaveSettings();
        AppServices.FileWatcher.RestartWatchers();
        HasUnsavedChanges = true;
    }

    partial void OnWatchDesktopChanged(bool value)
    {
        AppServices.FileWatcher.WatchDesktop = value;
        AppServices.FileWatcher.SaveSettings();
        AppServices.FileWatcher.RestartWatchers();
        HasUnsavedChanges = true;
    }

    partial void OnWatchDocumentsChanged(bool value)
    {
        AppServices.FileWatcher.WatchDocuments = value;
        AppServices.FileWatcher.SaveSettings();
        AppServices.FileWatcher.RestartWatchers();
        HasUnsavedChanges = true;
    }

    partial void OnIsOverlayWidgetEnabledChanged(bool value)
    {
        IsOverlayWidgetEnabledSetting = value;
        HasUnsavedChanges = true;
    }

    partial void OnOverlayPositionIndexChanged(int value)
    {
        OverlayPositionSetting = value;
        HasUnsavedChanges = true;
    }

    // ==========================================
    // Commands
    // ==========================================

    public string Text(string key) => AppServices.Localization.GetString(key);

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
    public void SaveAllChanges()
    {
        // Persist café profile settings
        AppServices.Database.SetSetting("shop_name", ShopName);
        AppServices.Database.SetSetting("operator_name", OperatorName);
        AppServices.Database.SetSetting("csc_vle_id", CscVleId);
        AppServices.Database.SetSetting("contact_number", ContactNumber);
        AppServices.Database.SetSetting("shop_address", ShopAddress);
        AppServices.Database.SetSetting("shop_upi_vpa", ShopUpiVpa);
        AppServices.Database.SetSetting("shop_upi_id", ShopUpiVpa); // Keep both keys synchronized
        AppServices.Database.SetSetting("payee_name", PayeeName);

        // Persist hardware settings
        AppServices.Database.SetSetting("default_bw_printer", DefaultBwPrinter);
        AppServices.Database.SetSetting("default_color_printer", DefaultColorPrinter);

        // Persist close behavior
        AppServices.Database.SetSetting("close_behavior", SelectedCloseBehaviorIndex.ToString());

        HasUnsavedChanges = false;
        ShowSuccess("All settings saved.");
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
            if (!string.IsNullOrWhiteSpace(folderPath))
            {
                WorkingRootPath = folderPath;
                AppServices.FolderManager.SetBaseDirectory(folderPath);
                AppServices.Database.SetSetting("working_root_path", folderPath);
                AppServices.FileWatcher.RestartWatchers();
                ShowSuccess("Working folder changed successfully.");
            }
        }
        catch (Exception ex)
        {
            ShowError($"Error changing folder: {ex.Message}");
        }
    }

    [RelayCommand]
    public async Task BrowseBackupFolderAsync()
    {
        try
        {
            var folderPath = await AppServices.Pickers.PickFolderAsync();
            if (!string.IsNullOrWhiteSpace(folderPath))
            {
                BackupRootPath = folderPath;
                AppServices.FolderManager.SetBackupDirectory(folderPath);
                AppServices.Database.SetSetting("backup_root_path", folderPath);
                ShowSuccess("Archive Database folder changed successfully.");
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
                    ShowSuccess($"Added watched folder: {Path.GetFileName(folderPath)}");
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
            ShowSuccess("Custom watched folder removed.");
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

        WatchDownloads = true;
        WatchDesktop = true;
        WatchDocuments = true;
        AutoArchiveDaysIndex = 2;

        SaveAllChanges();
        ShowSuccess("Defaults restored successfully.");
    }
}
