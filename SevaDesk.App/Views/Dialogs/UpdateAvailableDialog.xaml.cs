using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace SevaDesk_App.Views.Dialogs;

public sealed partial class UpdateAvailableDialog : ContentDialog
{
    private readonly string _assetUrl;
    private readonly string _fileName;
    private bool _isDownloading = false;
    private HttpClient? _httpClient;

    public UpdateAvailableDialog(string currentVersion, string newVersion, string changelog, string assetUrl, string fileName)
    {
        this.InitializeComponent();
        
        TxtVersionInfo.Text = $"A new version of SevaDesk (v{newVersion}) is available.\nYou are currently on v{currentVersion}.";
        TxtChangelog.Text = string.IsNullOrWhiteSpace(changelog) ? "No release notes provided." : changelog.Trim();
        
        _assetUrl = assetUrl;
        _fileName = fileName;
    }

    private async void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        if (_isDownloading)
        {
            args.Cancel = true;
            return;
        }

        // Prevent dialog from closing
        args.Cancel = true;
        _isDownloading = true;
        
        // Update UI
        IsPrimaryButtonEnabled = false;
        IsSecondaryButtonEnabled = false;
        ProgressPanel.Visibility = Visibility.Visible;
        TxtProgressStatus.Text = "Downloading update...";

        try
        {
            await DownloadAndInstallAsync();
        }
        catch (Exception ex)
        {
            TxtProgressStatus.Text = "Download failed.";
            TxtProgressDetails.Text = ex.Message;
            IsPrimaryButtonEnabled = true;
            IsSecondaryButtonEnabled = true;
            _isDownloading = false;
        }
    }

    private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        if (_isDownloading)
        {
            args.Cancel = true;
            _httpClient?.CancelPendingRequests();
        }
    }

    private async Task DownloadAndInstallAsync()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), _fileName);
        
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "SevaDesk-App");

        using var response = await _httpClient.GetAsync(_assetUrl, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? -1L;
        var canReportProgress = totalBytes != -1;

        using var stream = await response.Content.ReadAsStreamAsync();
        using var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

        var buffer = new byte[8192];
        var totalRead = 0L;
        var isMoreToRead = true;

        while (isMoreToRead)
        {
            var read = await stream.ReadAsync(buffer, 0, buffer.Length);
            if (read == 0)
            {
                isMoreToRead = false;
                continue;
            }

            await fileStream.WriteAsync(buffer, 0, read);
            totalRead += read;

            if (canReportProgress)
            {
                var percentage = (double)totalRead / totalBytes * 100;
                DownloadProgressBar.Value = percentage;
                TxtProgressDetails.Text = $"{totalRead / 1024 / 1024.0:F1} MB / {totalBytes / 1024 / 1024.0:F1} MB";
            }
            else
            {
                DownloadProgressBar.IsIndeterminate = true;
                TxtProgressDetails.Text = $"{totalRead / 1024 / 1024.0:F1} MB downloaded";
            }
        }

        TxtProgressStatus.Text = "Starting installer...";
        DownloadProgressBar.IsIndeterminate = true;
        
        // Wait a tiny bit for UI update
        await Task.Delay(500);

        // Launch the installer
        Process.Start(new ProcessStartInfo
        {
            FileName = tempPath,
            UseShellExecute = true
        });

        // Close app so installer can overwrite files
        Application.Current.Exit();
    }
}
