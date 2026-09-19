using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Controls;
using SevaDesk_App.Services;
using Windows.ApplicationModel.DataTransfer;

namespace SevaDesk_App.ViewModels.Pages;

public partial class AboutViewModel : StatusViewModel
{
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

    [RelayCommand]
    public async Task ResetAppDataAsync()
    {
        // Step 1 — initial confirmation
        var first = await AppServices.Dialogs.ShowConfirmationAsync(
            title: "Reset All App Data",
            content: "This will permanently delete ALL customer records, sessions, payments, documents, and settings stored in SevaDesk.\n\nThis action cannot be undone.",
            primaryButtonText: "Continue",
            secondaryButtonText: "Cancel");

        if (first != ContentDialogResult.Primary) return;

        // Step 2 — type-to-confirm guard
        var (result, typed) = await AppServices.Dialogs.ShowInputAsync(
            title: "Confirm Reset",
            content: "Type  RESET  in the box below to confirm you want to erase all data.",
            placeholderText: "RESET",
            primaryButtonText: "Erase Everything",
            secondaryButtonText: "Cancel");

        if (result != ContentDialogResult.Primary) return;
        if (!string.Equals(typed?.Trim(), "RESET", StringComparison.Ordinal))
        {
            ShowWarning("Reset cancelled — confirmation text did not match.");
            return;
        }

        try
        {
            // Stop all background services that hold file/db handles
            AppServices.FileWatcher.StopWatchers();

            // Resolve the database path from the connection string before closing it
            var connStr = AppServices.Database.ConnectionString;
            var dbPath = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder(connStr).DataSource;

            // Give SQLite WAL a moment to flush, then delete
            await Task.Delay(300);
            SqliteCloseAll();

            if (File.Exists(dbPath))
                File.Delete(dbPath);

            // Also wipe the Backups subfolder if present
            var backupDir = Path.Combine(Path.GetDirectoryName(dbPath) ?? string.Empty, "Backups");
            if (Directory.Exists(backupDir))
                Directory.Delete(backupDir, recursive: true);

            // Clear persisted settings (translations are kept — they are not user data)
            var appDataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SevaDesk");
            var settingsFile = Path.Combine(appDataDir, "settings.json");
            if (File.Exists(settingsFile)) File.Delete(settingsFile);

            // Restart the app immediately so it reinitialises with a clean database.
            try
            {
                // Works for MSIX-packaged apps (Windows App SDK)
                Microsoft.Windows.AppLifecycle.AppInstance.Restart(string.Empty);
            }
            catch
            {
                // Fallback for unpackaged / debug runs: re-launch the exe, then exit
                var exe = Environment.ProcessPath;
                if (!string.IsNullOrWhiteSpace(exe))
                    Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true });

                Microsoft.UI.Xaml.Application.Current.Exit();
            }
        }
        catch (Exception ex)
        {
            ShowError($"Reset failed: {ex.Message}");
        }
    }

    /// <summary>Closes all pooled SQLite connections so the .db file can be deleted.</summary>
    private static void SqliteCloseAll()
    {
        try { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); } catch { }
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
}
