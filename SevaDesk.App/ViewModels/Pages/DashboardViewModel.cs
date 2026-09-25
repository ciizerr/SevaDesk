using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SevaDesk.Core.Models;
using SevaDesk_App.Services;

namespace SevaDesk_App.ViewModels.Pages;

public partial class DashboardViewModel : StatusViewModel
{
    [ObservableProperty]
    private ObservableCollection<ActiveSessionItem> _activeSessions = [];

    [ObservableProperty]
    private ActiveSessionItem? _selectedSession;

    [ObservableProperty]
    private int _activeSessionsCount;

    [ObservableProperty]
    private int _totalPendingFiles;

    [ObservableProperty]
    private bool _hasActiveSessions;

    [ObservableProperty]
    private bool _isLoading;

    public async Task InitializeAsync()
    {
        await LoadActiveSessionsAsync();
    }

    [RelayCommand]
    public async Task LoadActiveSessionsAsync()
    {
        IsLoading = true;
        try
        {
            var sessions = await AppServices.Sessions.GetActiveSessionsAsync();
            ActiveSessions.Clear();

            int pendingFiles = 0;
            foreach (var item in sessions)
            {
                ActiveSessions.Add(item);
                pendingFiles += item.FolderStats.UnorganisedCount;
            }

            ActiveSessionsCount = ActiveSessions.Count;
            TotalPendingFiles = pendingFiles;
            HasActiveSessions = ActiveSessionsCount > 0;

            if (SelectedSession == null || !ActiveSessions.Any(s => s.Session.Id == SelectedSession.Session.Id))
            {
                SelectedSession = ActiveSessions.FirstOrDefault();
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public void SelectSession(ActiveSessionItem item)
    {
        SelectedSession = item;
    }

    [RelayCommand]
    public void OpenSelectedFolder()
    {
        if (SelectedSession != null)
        {
            AppServices.FolderManager.OpenFolderInExplorer(SelectedSession.FolderPath);
        }
    }

    [RelayCommand]
    public void OpenSubfolder(string subfolder)
    {
        if (SelectedSession != null)
        {
            var target = string.IsNullOrWhiteSpace(subfolder)
                ? SelectedSession.FolderPath
                : Path.Combine(SelectedSession.FolderPath, subfolder);
            AppServices.FolderManager.OpenFolderInExplorer(target);
        }
    }

    [RelayCommand]
    public async Task PauseSessionAsync(string sessionId)
    {
        await AppServices.Sessions.PauseSessionAsync(sessionId);
        await LoadActiveSessionsAsync();
    }

    [RelayCommand]
    public async Task ResumeSessionAsync(string sessionId)
    {
        await AppServices.Sessions.ResumeSessionAsync(sessionId);
        await LoadActiveSessionsAsync();
    }

    [RelayCommand]
    public async Task CompleteSessionAsync(string sessionId)
    {
        await AppServices.Sessions.CompleteSessionAsync(sessionId);
        await LoadActiveSessionsAsync();
    }

    [RelayCommand]
    public async Task DeleteSessionAsync(ActiveSessionItem item)
    {
        if (item == null) return;
        await AppServices.Sessions.DeleteSessionAsync(item.Session.Id);
        if (item.Customer != null)
        {
            AppServices.FolderManager.CleanUpEmptyCustomerWorkingFolder(item.Customer.Name, item.Customer.Code);
        }
        await LoadActiveSessionsAsync();
    }
}
