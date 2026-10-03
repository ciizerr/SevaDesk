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

    [ObservableProperty]
    private string _saveStatusText = "All changes saved";

    [ObservableProperty]
    private bool _isSaved = true;

    // ==========================================
    // Tab 1: Shop Profile
    // ==========================================
    [ObservableProperty]
    private string _shopName = string.Empty;

    [ObservableProperty]
    private string _operatorName = string.Empty;

    [ObservableProperty]
    private string _cscVleId = string.Empty;

    [ObservableProperty]
    private string _contactNumber = string.Empty;

    [ObservableProperty]
    private string _shopAddress = string.Empty;

    [ObservableProperty]
    private string _shopUpiVpa = string.Empty;

    [ObservableProperty]
    private string _payeeName = string.Empty;

    // ==========================================
    // Tab 2: Appearance, Material & Language
    // ==========================================
    [ObservableProperty]
    private int _selectedThemeIndex = 0; // 0 = System, 1 = Light, 2 = Dark

    [ObservableProperty]
    private bool _enableMicaBackdrop = true;

    [ObservableProperty]
    private bool _runOnStartup = false;

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
    // Tab 3: Storage & Automation
    // ==========================================
    [ObservableProperty]
    private string _workingRootPath = string.Empty;

    [ObservableProperty]
    private string _backupRootPath = string.Empty;

    [ObservableProperty]
    private double _workingFolderTtlHours = 24;

    [ObservableProperty]
    private string _workingFolderTtlDisplay = "24 hours (1 day)";

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

        _workingRootPath = AppServices.FolderManager.BaseDirectory;
        _backupRootPath = AppServices.FolderManager.BackupDirectory;
        _workingFolderTtlHours = SevaDesk_App.Services.Maintenance.WorkingFolderCleanupService.GetTtlHours();
        _workingFolderTtlDisplay = FormatTtlHours((int)Math.Round(_workingFolderTtlHours));

        // BUG FIX: Use backing-field assignment so OnWatchXxxChanged partial handlers
        // do NOT fire during construction (which was triggering the "Watched folders updated." toast).
        _watchDownloads = AppServices.FileWatcher.WatchDownloads;
        _watchDesktop = AppServices.FileWatcher.WatchDesktop;
        _watchDocuments = AppServices.FileWatcher.WatchDocuments;

        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _databasePath = Path.Combine(appData, "SevaDesk", "sevadesk.db");

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

        try
        {
            var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", false);
            _runOnStartup = key?.GetValue("SevaDesk") != null;
        }
        catch { }

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

    partial void OnRunOnStartupChanged(bool value)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
            if (key != null)
            {
                if (value)
                {
                    var exePath = Process.GetCurrentProcess().MainModule?.FileName;
                    if (!string.IsNullOrEmpty(exePath))
                    {
                        key.SetValue("SevaDesk", $"\"{exePath}\"");
                    }
                }
                else
                {
                    key.DeleteValue("SevaDesk", false);
                }
            }
            ShowSuccess(value ? "SevaDesk will start automatically with Windows." : "Auto-startup disabled.");
        }
        catch (Exception ex)
        {
            ShowError($"Could not change startup settings: {ex.Message}");
        }
    }

    partial void OnSelectedLanguageChanged(LanguageItem? value)
    {
        if (value != null && !string.Equals(value.Code, AppServices.Localization.CurrentLanguageCode, StringComparison.OrdinalIgnoreCase))
        {
            AppServices.Localization.SetLanguage(value.Code);
            ShowSuccess($"Language changed to {value.DisplayName}.");
        }
    }

    public static string FormatTtlHours(int hours)
    {
        if (hours <= 0) return "Immediately on next startup";
        if (hours == 1) return "1 hour";
        if (hours < 24) return $"{hours} hours";
        if (hours == 24) return "24 hours (1 day)";
        if (hours % 24 == 0)
        {
            var days = hours / 24;
            return $"{hours} hours ({days} {(days == 1 ? "day" : "days")})";
        }
        var d = hours / 24;
        var remH = hours % 24;
        return $"{hours} hours ({d}d {remH}h)";
    }

    partial void OnWorkingFolderTtlHoursChanged(double value)
    {
        var hours = Math.Max(0, (int)Math.Round(value));
        WorkingFolderTtlDisplay = FormatTtlHours(hours);
    }

    public void CommitWorkingFolderTtl(int hours)
    {
        hours = Math.Clamp(hours, 0, 720);
        WorkingFolderTtlHours = hours;
        WorkingFolderTtlDisplay = FormatTtlHours(hours);
        SevaDesk_App.Services.Maintenance.WorkingFolderCleanupService.SetTtlHours(hours);
        ShowSuccess(hours <= 0
            ? "Desktop folders will be cleaned immediately on next startup."
            : $"Desktop folders will be cleaned {FormatTtlHours(hours)} after backup sync.");
    }

    // ==========================================
    // Dirty-Flag Settings & Auto-Save
    // ==========================================

    private void MarkDirty()
    {
        HasUnsavedChanges = true;
        IsSaved = false;
        SaveStatusText = "Unsaved changes...";
    }

    partial void OnSelectedCloseBehaviorIndexChanged(int value)
    {
        CloseActionBehavior = value;
        MarkDirty();
    }

    partial void OnShopNameChanged(string value) => MarkDirty();
    partial void OnOperatorNameChanged(string value) => MarkDirty();
    partial void OnCscVleIdChanged(string value) => MarkDirty();
    partial void OnContactNumberChanged(string value) => MarkDirty();
    partial void OnShopAddressChanged(string value) => MarkDirty();
    partial void OnShopUpiVpaChanged(string value) => MarkDirty();
    partial void OnPayeeNameChanged(string value) => MarkDirty();

    partial void OnWatchDownloadsChanged(bool value)
    {
        AppServices.FileWatcher.WatchDownloads = value;
        AppServices.FileWatcher.SaveSettings();
        AppServices.FileWatcher.RestartWatchers();
        MarkDirty();
    }

    partial void OnWatchDesktopChanged(bool value)
    {
        AppServices.FileWatcher.WatchDesktop = value;
        AppServices.FileWatcher.SaveSettings();
        AppServices.FileWatcher.RestartWatchers();
        MarkDirty();
    }

    partial void OnWatchDocumentsChanged(bool value)
    {
        AppServices.FileWatcher.WatchDocuments = value;
        AppServices.FileWatcher.SaveSettings();
        AppServices.FileWatcher.RestartWatchers();
        MarkDirty();
    }

    partial void OnIsOverlayWidgetEnabledChanged(bool value)
    {
        IsOverlayWidgetEnabledSetting = value;
        MarkDirty();
    }

    partial void OnOverlayPositionIndexChanged(int value)
    {
        OverlayPositionSetting = value;
        MarkDirty();
    }

    [RelayCommand]
    public void AutoSaveProfile()
    {
        if (!HasUnsavedChanges) return;

        AppServices.Database.SetSetting("shop_name", ShopName);
        AppServices.Database.SetSetting("operator_name", OperatorName);
        AppServices.Database.SetSetting("csc_vle_id", CscVleId);
        AppServices.Database.SetSetting("contact_number", ContactNumber);
        AppServices.Database.SetSetting("shop_address", ShopAddress);
        AppServices.Database.SetSetting("shop_upi_vpa", ShopUpiVpa);
        AppServices.Database.SetSetting("shop_upi_id", ShopUpiVpa); // Keep both keys synchronized
        AppServices.Database.SetSetting("payee_name", PayeeName);
        AppServices.Database.SetSetting("close_behavior", SelectedCloseBehaviorIndex.ToString());

        HasUnsavedChanges = false;
        IsSaved = true;
        SaveStatusText = "All changes saved";
    }

    public void ReloadShopProfile()
    {
        ShopName = AppServices.Database.GetSetting("shop_name", ShopName) ?? ShopName;
        OperatorName = AppServices.Database.GetSetting("operator_name", OperatorName) ?? OperatorName;
        CscVleId = AppServices.Database.GetSetting("csc_vle_id", CscVleId) ?? CscVleId;
        ContactNumber = AppServices.Database.GetSetting("contact_number", ContactNumber) ?? ContactNumber;
        ShopAddress = AppServices.Database.GetSetting("shop_address", ShopAddress) ?? ShopAddress;
        ShopUpiVpa = AppServices.Database.GetSetting("shop_upi_vpa")
                     ?? AppServices.Database.GetSetting("shop_upi_id", ShopUpiVpa)
                     ?? ShopUpiVpa;
        PayeeName = AppServices.Database.GetSetting("payee_name", PayeeName) ?? PayeeName;

        HasUnsavedChanges = false;
        IsSaved = true;
        SaveStatusText = "Settings updated";
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
        AutoSaveProfile();
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
    public async Task BackupDatabaseAsync()
    {
        try
        {
            if (!File.Exists(DatabasePath))
            {
                ShowWarning("Database file not found to backup.");
                return;
            }

            var suggestedName = $"sevadesk_backup_{DateTime.Now:yyyy-MM-dd}.db";
            var destination = await AppServices.Pickers.PickSaveFileAsync(
                suggestedName, ".db", "SQLite Database (*.db)");

            if (string.IsNullOrWhiteSpace(destination))
            {
                // User cancelled file picker
                return;
            }

            // Ensure destination directory exists
            var destDir = Path.GetDirectoryName(destination);
            if (!string.IsNullOrWhiteSpace(destDir))
            {
                Directory.CreateDirectory(destDir);
            }

            // Flush SQLite connection pool so we copy complete, committed data
            try { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); } catch { }
            File.Copy(DatabasePath, destination, overwrite: true);

            ShowSuccess($"Backup saved successfully: {Path.GetFileName(destination)}");
        }
        catch (Exception ex)
        {
            ShowError($"Backup failed: {ex.Message}");
        }
    }

    [RelayCommand]
    public async Task RestoreDatabaseAsync()
    {
        try
        {
            var pickedPath = await AppServices.Pickers.PickFileAsync(new[] { ".db" });
            if (string.IsNullOrWhiteSpace(pickedPath))
            {
                // User cancelled file picker
                return;
            }

            if (!File.Exists(pickedPath))
            {
                ShowWarning("Selected backup file could not be found.");
                return;
            }

            var fileInfo = new FileInfo(pickedPath);
            if (fileInfo.Length == 0)
            {
                ShowWarning("Selected backup file is empty and cannot be restored.");
                return;
            }

            // Confirmation warning
            var confirm = await AppServices.Dialogs.ShowConfirmationAsync(
                title: "Restore Database Backup",
                content: $"Restoring this backup will replace your current customer records and sessions with:\n{Path.GetFileName(pickedPath)}\n\nAn automatic safety backup of your current database will be saved before restoring.\n\nThe app will restart immediately to load your restored records. Do you wish to continue?",
                primaryButtonText: "Restore & Restart",
                secondaryButtonText: "Cancel");

            if (confirm != Microsoft.UI.Xaml.Controls.ContentDialogResult.Primary)
            {
                return;
            }

            // 1. Stop background file watchers
            AppServices.FileWatcher.StopWatchers();

            // 2. Create safety snapshot of current database
            if (File.Exists(DatabasePath))
            {
                var dir = Path.GetDirectoryName(DatabasePath) ?? string.Empty;
                var backupDir = Path.Combine(dir, "Backups");
                Directory.CreateDirectory(backupDir);
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var safetyPath = Path.Combine(backupDir, $"sevadesk_safety_before_restore_{timestamp}.db");
                File.Copy(DatabasePath, safetyPath, overwrite: true);
            }

            // 3. Clear SQLite pools and wait briefly
            try { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); } catch { }
            await Task.Delay(300);

            // 4. Overwrite DatabasePath with the picked backup file
            File.Copy(pickedPath, DatabasePath, overwrite: true);

            // 5. Restart application
            try
            {
                Microsoft.Windows.AppLifecycle.AppInstance.Restart(string.Empty);
            }
            catch
            {
                var exe = Environment.ProcessPath;
                if (!string.IsNullOrWhiteSpace(exe))
                {
                    Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true });
                }
                Microsoft.UI.Xaml.Application.Current.Exit();
            }
        }
        catch (Exception ex)
        {
            ShowError($"Restore failed: {ex.Message}");
        }
    }

    [RelayCommand]
    public void ResetDefaults()
    {
        SelectedThemeIndex = 0;
        EnableMicaBackdrop = true;
        CloseActionBehavior = 0;
        SelectedCloseBehaviorIndex = 0;

        ShopName = string.Empty;
        OperatorName = string.Empty;
        CscVleId = string.Empty;
        ContactNumber = string.Empty;
        ShopAddress = string.Empty;
        ShopUpiVpa = string.Empty;
        PayeeName = string.Empty;

        WatchDownloads = true;
        WatchDesktop = true;
        WatchDocuments = true;
        AutoArchiveDaysIndex = 2;

        SaveAllChanges();
        ShowSuccess("Defaults restored successfully.");
    }
}
