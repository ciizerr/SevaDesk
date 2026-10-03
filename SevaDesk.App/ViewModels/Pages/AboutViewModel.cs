using System.Diagnostics;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Controls;
using SevaDesk_App.Services;

namespace SevaDesk_App.ViewModels.Pages;

public partial class AboutViewModel : StatusViewModel
{
    [ObservableProperty]
    private string _appVersionDisplay = GetAppVersion();

    private static string GetAppVersion()
    {
        var attr = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>();
        if (attr != null && !string.IsNullOrEmpty(attr.InformationalVersion))
        {
            var v = attr.InformationalVersion.Split('+')[0]; // Remove git commit hash if present
            // Ensure first letter of pre-release tag is uppercase (e.g. beta -> Beta)
            if (v.Contains('-'))
            {
                var parts = v.Split('-');
                if (parts.Length > 1 && parts[1].Length > 0)
                {
                    parts[1] = char.ToUpper(parts[1][0]) + parts[1].Substring(1);
                    v = string.Join("-", parts);
                }
            }
            return $"v{v}";
        }
        var ver = Assembly.GetExecutingAssembly().GetName().Version;
        return ver != null ? $"v{ver.Major}.{ver.Minor}.{ver.Build}" : "vUnknown";
    }

    [ObservableProperty]
    private string _buildInfoDisplay = "Offline Desktop Edition · Windows 10 & 11";

    [ObservableProperty]
    private string _developerName = "ciizerr";


    [ObservableProperty]
    private string _githubUrl = "https://github.com/ciizerr/SevaDesk";

    [ObservableProperty]
    private string _bugReportUrl = "https://github.com/ciizerr/SevaDesk/issues/new?template=bug_report.md";

    [ObservableProperty]
    private string _featureRequestUrl = "https://github.com/ciizerr/SevaDesk/issues/new?template=feature_request.md";

    [ObservableProperty]
    private string _communityUrl = "https://github.com/ciizerr/SevaDesk/discussions";


    [RelayCommand]
    public async Task CheckUpdatesAsync()
    {
        await UpdateService.CheckForUpdatesAsync(silent: false);
    }

    [RelayCommand]
    public void OpenGitHub() => OpenUrl(GithubUrl);

    [RelayCommand]
    public void ReportBug() => OpenUrl(BugReportUrl);

    [RelayCommand]
    public void RequestFeature() => OpenUrl(FeatureRequestUrl);

    [RelayCommand]
    public void OpenCommunity() => OpenUrl(CommunityUrl);
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
