using System;
using System.Diagnostics;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Controls;

namespace SevaDesk_App.Services;

public static class UpdateService
{
    private const string GITHUB_API_LATEST_RELEASE = "https://api.github.com/repos/ciizerr/SevaDesk/releases/latest";

    public static async Task CheckForUpdatesAsync(bool silent = false)
    {
        try
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", "SevaDesk-App");
            client.Timeout = TimeSpan.FromSeconds(10);

            var response = await client.GetAsync(GITHUB_API_LATEST_RELEASE);
            if (!response.IsSuccessStatusCode)
            {
                if (!silent)
                {
                    await AppServices.Dialogs.ShowAlertAsync(
                        "Update Check Failed", 
                        $"Could not connect to GitHub to check for updates. (Status: {response.StatusCode})");
                }
                return;
            }

            var jsonString = await response.Content.ReadAsStringAsync();
            using var json = JsonDocument.Parse(jsonString);
            
            if (json.RootElement.TryGetProperty("tag_name", out var tagElement))
            {
                var latestVersionString = tagElement.GetString()?.TrimStart('v', 'V');
                if (Version.TryParse(latestVersionString, out var latestVersion))
                {
                    var currentVersionStr = Assembly.GetExecutingAssembly().GetName().Version?.ToString();
                    var currentVersion = Version.Parse(currentVersionStr ?? "0.1.0.0");

                    if (latestVersion > currentVersion)
                    {
                        var dialogResult = await AppServices.Dialogs.ShowConfirmationAsync(
                            title: "Update Available! \uD83C\uDF89",
                            content: $"A new version of SevaDesk (v{latestVersion}) is available.\nYou are currently on v{currentVersion}.\n\nWould you like to download it now?",
                            primaryButtonText: "Download Update",
                            secondaryButtonText: "Later"
                        );

                        if (dialogResult == ContentDialogResult.Primary)
                        {
                            var releaseUrl = json.RootElement.GetProperty("html_url").GetString();
                            if (!string.IsNullOrEmpty(releaseUrl))
                            {
                                Process.Start(new ProcessStartInfo
                                {
                                    FileName = releaseUrl,
                                    UseShellExecute = true
                                });
                            }
                        }
                    }
                    else
                    {
                        if (!silent)
                        {
                            await AppServices.Dialogs.ShowAlertAsync("Up to Date", $"You are running the latest version of SevaDesk (v{currentVersion}).");
                        }
                    }
                }
                else if (!silent)
                {
                    await AppServices.Dialogs.ShowAlertAsync("Update Check Error", "Could not parse the version number from the latest release on GitHub.");
                }
            }
        }
        catch (Exception ex)
        {
            if (!silent)
            {
                await AppServices.Dialogs.ShowAlertAsync("Update Check Error", $"An error occurred while checking for updates:\n{ex.Message}");
            }
        }
    }
}
