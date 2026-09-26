using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SevaDesk.Core.Models;
using SevaDesk_App.Services;
using Windows.System;

namespace SevaDesk_App.ViewModels.Pages;

public partial class SessionsViewModel : StatusViewModel
{
    [ObservableProperty]
    private ObservableCollection<ActiveSessionItem> _sessions = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedSession))]
    private ActiveSessionItem? _selectedSession;

    public bool HasSelectedSession => SelectedSession != null;

    [ObservableProperty]
    private int _activeCount;

    [ObservableProperty]
    private int _pausedCount;

    public ObservableCollection<string> ApplicationFolders { get; } = [];

    [ObservableProperty]
    private IncomingFileItem? _pendingIncomingFile;

    [ObservableProperty]
    private bool _hasPendingIncomingFile;

    // --- Application Integration ---
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasActiveApplication))]
    private ApplicationItem? _activeApplication;

    public bool HasActiveApplication => ActiveApplication != null;

    public ObservableCollection<ApplicationItem> CustomerApplications { get; } = [];

    public bool HasMultipleApplications => CustomerApplications.Count > 1;

    [ObservableProperty]
    private bool _isApplicationCardCollapsed = false;

    [RelayCommand]
    public void ToggleApplicationCardCollapse()
    {
        IsApplicationCardCollapsed = !IsApplicationCardCollapsed;
    }

    [RelayCommand]
    public void SelectApplication(ApplicationItem app)
    {
        if (app != null)
        {
            ActiveApplication = app;
            _ = RefreshActiveChecklistAsync();
        }
    }

    private readonly SemaphoreSlim _loadLock = new(1, 1);
    public string? PendingPreferredSessionId { get; set; }

    public ObservableCollection<ApplicationChecklistItem> ActiveChecklist { get; } = [];

    public void Initialize()
    {
        _ = LoadSessionsAsync();
        AppServices.FileWatcher.FileDetected += FileWatcher_FileDetected;
        AppServices.Sessions.SessionsChanged += (s, e) =>
        {
            if (MainWindow.Instance?.DispatcherQueue != null)
            {
                MainWindow.Instance.DispatcherQueue.TryEnqueue(async () =>
                {
                    await LoadSessionsAsync();
                });
            }
            else
            {
                _ = LoadSessionsAsync();
            }
        };
    }

    private void FileWatcher_FileDetected(IncomingFileItem item)
    {
        if (SelectedSession != null)
        {
            MainWindow.Instance?.DispatcherQueue.TryEnqueue(() =>
            {
                PendingIncomingFile = item;
                HasPendingIncomingFile = true;
            });
        }
    }

    partial void OnSelectedSessionChanged(ActiveSessionItem? value)
    {
        RefreshApplicationFolders();
        _ = LoadCustomerApplicationAsync();
    }

    public async Task LoadCustomerApplicationAsync()
    {
        ActiveChecklist.Clear();
        CustomerApplications.Clear();
        if (SelectedSession?.Customer == null)
        {
            ActiveApplication = null;
            OnPropertyChanged(nameof(HasMultipleApplications));
            return;
        }

        var apps = (await AppServices.Applications.GetByCustomerIdAsync(SelectedSession.Customer.Id)).ToList();
        // Do NOT automatically show past Completed applications in a new session
        var pendingApps = apps.Where(a => a.Status != "Completed").ToList();
        foreach (var app in pendingApps)
        {
            CustomerApplications.Add(app);
        }

        ActiveApplication = pendingApps.FirstOrDefault();
        OnPropertyChanged(nameof(HasMultipleApplications));
        _ = RefreshActiveChecklistAsync();
    }

    public async Task RefreshActiveChecklistAsync()
    {
        if (ActiveApplication == null || string.IsNullOrWhiteSpace(ActiveApplication.RequiredDocs))
        {
            ActiveChecklist.Clear();
            return;
        }

        string? workingFolder = SelectedSession?.FolderPath;
        string? backupFolder = null;
        if (SelectedSession?.Customer != null)
        {
            backupFolder = AppServices.FolderManager.GetCustomerBackupFolderPath(
                SelectedSession.Customer.Name, SelectedSession.Customer.Code);
        }

        var result = await ApplicationDocumentVerifier.CheckAndAutoUpdateStatusAsync(
            ActiveApplication, workingFolder, backupFolder);

        ActiveChecklist.Clear();
        foreach (var item in result.Checklist)
        {
            ActiveChecklist.Add(item);
        }

        OnPropertyChanged(nameof(ActiveApplication));
    }

    public async Task OnChecklistItemToggledAsync()
    {
        if (ActiveApplication == null) return;
        bool allChecked = ActiveChecklist.Count > 0 && ActiveChecklist.All(i => i.IsCompleted);
        if (allChecked && ActiveApplication.Status == "Draft")
        {
            ActiveApplication.Status = "Docs Ready";
            ActiveApplication.UpdatedAt = DateTime.UtcNow;
            await AppServices.Applications.UpdateAsync(ActiveApplication);
            OnPropertyChanged(nameof(ActiveApplication));
        }
        else if (!allChecked && ActiveApplication.Status == "Docs Ready")
        {
            ActiveApplication.Status = "Draft";
            ActiveApplication.UpdatedAt = DateTime.UtcNow;
            await AppServices.Applications.UpdateAsync(ActiveApplication);
            OnPropertyChanged(nameof(ActiveApplication));
        }
    }

    [RelayCommand]
    public async Task OpenPortalUrl(string portal)
    {
        if (string.IsNullOrWhiteSpace(portal)) return;
        var url = portal.StartsWith("http") ? portal : $"https://{portal}";
        try
        {
            await Launcher.LaunchUriAsync(new Uri(url));
        }
        catch { }
    }

    [RelayCommand]
    public async Task UpdateApplicationStatusAsync(string newStatus)
    {
        if (ActiveApplication == null) return;
        ActiveApplication.Status = newStatus;
        ActiveApplication.UpdatedAt = DateTime.UtcNow;
        await AppServices.Applications.UpdateAsync(ActiveApplication);
        _ = RefreshActiveChecklistAsync();
        if (SelectedSession != null)
        {
            SelectedSession.NotifyLinkedApplicationChanged();
        }
        OnPropertyChanged(nameof(ActiveApplication));
    }

    [RelayCommand]
    public async Task LinkApplicationAsync(ApplicationItem app)
    {
        if (app == null || SelectedSession?.Customer == null) return;
        app.CustomerId = SelectedSession.Customer.Id;
        app.CustomerName = SelectedSession.Customer.Name;
        app.SessionId = SelectedSession.Session.Id;
        var created = await AppServices.Applications.CreateAsync(app);
        CustomerApplications.Add(created);
        ActiveApplication = created;
        SelectedSession.LinkedApplication = created;
        SelectedSession.NotifyLinkedApplicationChanged();
        IsApplicationCardCollapsed = false;
        OnPropertyChanged(nameof(HasMultipleApplications));
        _ = RefreshActiveChecklistAsync();

        // Also ensure an application folder exists for this scheme
        if (!string.IsNullOrWhiteSpace(created.Title))
        {
            var folderSafe = string.Join("_", created.Title.Split(Path.GetInvalidFileNameChars())).Trim();
            AppServices.FolderManager.EnsureApplicationSubfolder(SelectedSession.FolderPath, folderSafe);
            RefreshApplicationFolders();
        }
    }


    public void RefreshApplicationFolders()
    {
        ApplicationFolders.Clear();
        if (SelectedSession != null && Directory.Exists(SelectedSession.FolderPath))
        {
            foreach (var sub in AppServices.FolderManager.GetApplicationSubfolders(SelectedSession.FolderPath))
            {
                ApplicationFolders.Add(sub);
            }
            SelectedSession.FolderStats = AppServices.FolderManager.GetFolderStats(SelectedSession.FolderPath);
            OnPropertyChanged(nameof(SelectedSession));
        }
    }

    [RelayCommand]
    public void CreateApplicationFolder(string folderName)
    {
        if (SelectedSession != null && !string.IsNullOrWhiteSpace(folderName))
        {
            AppServices.FolderManager.EnsureApplicationSubfolder(SelectedSession.FolderPath, folderName);
            RefreshApplicationFolders();
        }
    }

    [RelayCommand]
    public void OpenSubfolder(string subfolderName)
    {
        if (SelectedSession == null) return;
        var path = string.IsNullOrWhiteSpace(subfolderName)
            ? SelectedSession.FolderPath
            : Path.Combine(SelectedSession.FolderPath, subfolderName);
        AppServices.FolderManager.OpenFolderInExplorer(path);
    }

    [RelayCommand]
    public async Task RoutePendingFileAsync()
    {
        if (PendingIncomingFile != null && SelectedSession != null)
        {
            var success = await AppServices.FileWatcher.RouteFileToCustomerAsync(
                PendingIncomingFile.FilePath,
                SelectedSession.FolderPath,
                deleteSource: true);

            if (success)
            {
                RefreshApplicationFolders();
            }

            HasPendingIncomingFile = false;
            PendingIncomingFile = null;
        }
    }

    [RelayCommand]
    public void DismissPendingFile()
    {
        HasPendingIncomingFile = false;
        PendingIncomingFile = null;
    }

    [RelayCommand]
    public async Task LoadSessionsAsync(string? preferredSessionId = null)
    {
        await _loadLock.WaitAsync();
        try
        {
            var targetSessionId = preferredSessionId ?? PendingPreferredSessionId ?? SelectedSession?.Session.Id;
            PendingPreferredSessionId = null;

            var activeSessions = (await AppServices.Sessions.GetActiveSessionsAsync()).ToList();
            var activeIds = new HashSet<string>(activeSessions.Select(s => s.Session.Id));

            // 1. Remove closed or deleted sessions
            for (int i = Sessions.Count - 1; i >= 0; i--)
            {
                if (!activeIds.Contains(Sessions[i].Session.Id))
                {
                    Sessions.RemoveAt(i);
                }
            }

            // 2. Add or update active sessions without duplicates
            for (int i = 0; i < activeSessions.Count; i++)
            {
                var fresh = activeSessions[i];
                var existingIndex = -1;
                for (int j = 0; j < Sessions.Count; j++)
                {
                    if (Sessions[j].Session.Id == fresh.Session.Id)
                    {
                        existingIndex = j;
                        break;
                    }
                }

                if (existingIndex == -1)
                {
                    if (i <= Sessions.Count)
                    {
                        Sessions.Insert(i, fresh);
                    }
                    else
                    {
                        Sessions.Add(fresh);
                    }
                }
                else
                {
                    var existing = Sessions[existingIndex];
                    existing.Session.Status = fresh.Session.Status;
                    existing.Session.DurationSeconds = fresh.Session.DurationSeconds;
                    existing.Session.StartedAt = fresh.Session.StartedAt;
                    existing.Customer = fresh.Customer;
                    existing.FolderPath = fresh.FolderPath;
                    existing.FolderStats = fresh.FolderStats;
                    existing.LinkedApplication = fresh.LinkedApplication;
                    existing.NotifyStatusChanged();

                    if (existingIndex != i && i < Sessions.Count)
                    {
                        Sessions.Move(existingIndex, i);
                    }
                }
            }

            ActiveCount = Sessions.Count(s => s.Session.Status == "Active");
            PausedCount = Sessions.Count(s => s.Session.Status == "Paused");

            // 3. Preserve or update selection
            if (!string.IsNullOrEmpty(targetSessionId))
            {
                var matched = Sessions.FirstOrDefault(s => s.Session.Id == targetSessionId);
                if (matched != null)
                {
                    SelectedSession = matched;
                }
                else if (SelectedSession == null || !Sessions.Contains(SelectedSession))
                {
                    SelectedSession = Sessions.FirstOrDefault();
                }
            }
            else if (SelectedSession == null || !Sessions.Contains(SelectedSession))
            {
                SelectedSession = Sessions.FirstOrDefault();
            }
        }
        finally
        {
            _loadLock.Release();
        }
    }

    [RelayCommand]
    public void OpenFolder(string folderPath)
    {
        AppServices.FolderManager.OpenFolderInExplorer(folderPath);
    }

    [RelayCommand]
    public async Task ToggleSessionStatusAsync(ActiveSessionItem item)
    {
        if (item == null) return;
        if (item.Session.Status == "Active")
        {
            var startedUtc = item.Session.StartedAt.Kind == DateTimeKind.Utc
                ? item.Session.StartedAt
                : (item.Session.StartedAt.Kind == DateTimeKind.Unspecified
                    ? DateTime.SpecifyKind(item.Session.StartedAt, DateTimeKind.Utc)
                    : item.Session.StartedAt.ToUniversalTime());
            var activeSec = Math.Max(0, (int)(DateTime.UtcNow - startedUtc).TotalSeconds);
            item.Session.DurationSeconds += activeSec;
            item.Session.Status = "Paused";

            AppServices.FileWatcher.ClearAutoRouteIfSession(item.Session.Id);
            await AppServices.Sessions.PauseSessionAsync(item.Session.Id, item.Session.DurationSeconds);
        }
        else
        {
            item.Session.StartedAt = DateTime.UtcNow;
            item.Session.Status = "Active";

            await AppServices.Sessions.ResumeSessionAsync(item.Session.Id);
        }

        ActiveCount = Sessions.Count(s => s.Session.Status == "Active");
        PausedCount = Sessions.Count(s => s.Session.Status == "Paused");
        item.NotifyStatusChanged();
        item.UpdateElapsed();
        OnPropertyChanged(nameof(Sessions));
        OnPropertyChanged(nameof(SelectedSession));
    }

    [RelayCommand]
    public async Task CompleteSessionAsync(ActiveSessionItem item)
    {
        if (item == null) return;
        if (ActiveApplication != null)
        {
            await ApplicationDocumentVerifier.CompleteApplicationAsync(ActiveApplication);
            OnPropertyChanged(nameof(ActiveApplication));
        }
        AppServices.FileWatcher.ClearAutoRouteIfSession(item.Session.Id);
        await AppServices.Sessions.CompleteSessionAsync(item.Session.Id);
        Sessions.Remove(item);
        ActiveCount = Sessions.Count(s => s.Session.Status == "Active");
        PausedCount = Sessions.Count(s => s.Session.Status == "Paused");
        if (SelectedSession == item)
        {
            SelectedSession = Sessions.FirstOrDefault();
        }
    }

    [RelayCommand]
    public async Task DeleteSessionAsync(ActiveSessionItem item)
    {
        if (item == null) return;
        AppServices.FileWatcher.ClearAutoRouteIfSession(item.Session.Id);

        // Clean up empty scheme subfolder if created
        if (item.LinkedApplication != null && !string.IsNullOrWhiteSpace(item.LinkedApplication.Title))
        {
            AppServices.FolderManager.CleanUpEmptyApplicationSubfolder(item.FolderPath, item.LinkedApplication.Title);
        }

        await AppServices.Sessions.DeleteSessionAsync(item.Session.Id);

        if (item.Customer != null)
        {
            AppServices.FolderManager.CleanUpEmptyCustomerWorkingFolder(item.Customer.Name, item.Customer.Code);
        }

        Sessions.Remove(item);
        ActiveCount = Sessions.Count(s => s.Session.Status == "Active");
        PausedCount = Sessions.Count(s => s.Session.Status == "Paused");
        if (SelectedSession == item)
        {
            SelectedSession = Sessions.FirstOrDefault();
        }
    }

    [RelayCommand]
    public async Task RemoveApplicationAsync(ApplicationItem app)
    {
        if (app == null) return;

        // Clean up empty scheme subfolder if created
        if (SelectedSession != null && !string.IsNullOrWhiteSpace(app.Title))
        {
            AppServices.FolderManager.CleanUpEmptyApplicationSubfolder(SelectedSession.FolderPath, app.Title);
            RefreshApplicationFolders();
        }

        await AppServices.Applications.DeleteAsync(app.Id);
        CustomerApplications.Remove(app);
        ActiveApplication = CustomerApplications.FirstOrDefault();
        if (SelectedSession != null)
        {
            SelectedSession.LinkedApplication = ActiveApplication;
            SelectedSession.NotifyLinkedApplicationChanged();
        }
        OnPropertyChanged(nameof(HasMultipleApplications));
        _ = RefreshActiveChecklistAsync();
    }

}
