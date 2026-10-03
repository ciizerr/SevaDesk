using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SevaDesk.Core.Models;
using SevaDesk_App.Services;

namespace SevaDesk_App.ViewModels.Pages;

public partial class CustomerWorkspaceViewModel : StatusViewModel
{
    [ObservableProperty]
    private Customer _customer = default!;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private ObservableCollection<Session> _sessions = [];

    [ObservableProperty]
    private ObservableCollection<CustomerSessionRowModel> _sessionHistory = [];

    [ObservableProperty]
    private ObservableCollection<Payment> _payments = [];

    [ObservableProperty]
    private ObservableCollection<CustomerTimelineItemModel> _timelineItems = [];

    [ObservableProperty]
    private ObservableCollection<FolderFileItem> _folderFiles = [];

    [ObservableProperty]
    private ObservableCollection<FolderGroup> _folderGroups = [];

    [ObservableProperty]
    private ObservableCollection<FolderGroup> _filteredFolderGroups = [];

    [ObservableProperty]
    private string _fileSearchQuery = string.Empty;

    [ObservableProperty]
    private int _selectedActivityTab = 0; // 0 = Bills & Receipts, 1 = Sessions, 2 = All Activity

    [ObservableProperty]
    private FolderStats _folderStats = new();

    [ObservableProperty]
    private bool _isShowingBackup;

    public bool HasPayments => Payments.Count > 0;
    public bool HasSessions => SessionHistory.Count > 0;
    public bool HasTimelineItems => TimelineItems.Count > 0;
    public bool HasFiles => FilteredFolderGroups.Count > 0;

    public CustomerWorkspaceViewModel()
    {
    }

    public async Task InitializeAsync(Customer customer)
    {
        Customer = customer;
        await LoadCustomerDetailsAsync();
    }

    public async Task RefreshAsync() => await LoadCustomerDetailsAsync();

    [RelayCommand]
    private async Task LoadCustomerDetailsAsync()
    {
        if (Customer == null) return;

        Sessions.Clear();
        Payments.Clear();
        SessionHistory.Clear();
        TimelineItems.Clear();
        FolderFiles.Clear();
        FolderGroups.Clear();
        FilteredFolderGroups.Clear();
        FolderStats = new FolderStats();

        IsLoading = true;
        try
        {
            // Load Sessions
            var sessions = (await AppServices.Sessions.GetCustomerSessionsAsync(Customer.Id)).ToList();
            foreach (var s in sessions) Sessions.Add(s);

            // Load Payments
            var payments = (await AppServices.Payments.GetCustomerPaymentsAsync(Customer.Id)).ToList();
            foreach (var p in payments) Payments.Add(p);

            // Correlate sessions with payments for rich history display
            foreach (var s in sessions.OrderByDescending(x => x.StartedAt))
            {
                var matchingPayment = payments.FirstOrDefault(p => p.SessionId == s.Id);
                SessionHistory.Add(new CustomerSessionRowModel(s, matchingPayment));
            }

            // Build unified timeline
            var merged = new List<CustomerTimelineItemModel>();
            foreach (var p in payments)
            {
                merged.Add(new CustomerTimelineItemModel(p));
            }
            foreach (var s in SessionHistory)
            {
                merged.Add(new CustomerTimelineItemModel(s));
            }
            foreach (var item in merged.OrderByDescending(x => x.Timestamp))
            {
                TimelineItems.Add(item);
            }

            // Load Files (both flat and grouped)
            RefreshFiles();

            NotifyStatsChanged();
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(HasPayments));
            OnPropertyChanged(nameof(HasSessions));
            OnPropertyChanged(nameof(HasTimelineItems));
            OnPropertyChanged(nameof(HasFiles));
        }
    }

    partial void OnFileSearchQueryChanged(string value)
    {
        ApplyFileFilter();
    }

    public void ApplyFileFilter()
    {
        FilteredFolderGroups.Clear();
        if (string.IsNullOrWhiteSpace(FileSearchQuery))
        {
            foreach (var g in FolderGroups)
            {
                FilteredFolderGroups.Add(g);
            }
        }
        else
        {
            var query = FileSearchQuery.Trim();
            foreach (var g in FolderGroups)
            {
                var matched = g.Files.Where(f => f.Name.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
                if (matched.Count > 0)
                {
                    var filteredGroup = new FolderGroup
                    {
                        FolderName = g.FolderName,
                        FolderPath = g.FolderPath,
                        Glyph = g.Glyph,
                        AccentColor = g.AccentColor
                    };
                    foreach (var mf in matched)
                    {
                        filteredGroup.Files.Add(mf);
                    }
                    FilteredFolderGroups.Add(filteredGroup);
                }
            }
        }
        OnPropertyChanged(nameof(HasFiles));
    }

    /// <summary>Re-scan folder and refresh both FolderFiles and FolderGroups. Safe to call from DispatcherQueue.</summary>
    public void RefreshFiles()
    {
        if (Customer == null) return;
        var folderPath = AppServices.FolderManager.GetEffectiveCustomerFolderPath(Customer.Name, Customer.Code, out bool isBackup);
        IsShowingBackup = isBackup;
        FolderStats = AppServices.FolderManager.GetFolderStats(folderPath);

        // Flat list (all files across root and subfolders)
        FolderFiles.Clear();
        var files = AppServices.FolderManager.GetFolderFiles(folderPath);
        foreach (var f in files) FolderFiles.Add(f);

        // Grouped list (used in CustomerWorkspacePage file viewer)
        FolderGroups.Clear();
        var groups = AppServices.FolderManager.GetFolderFilesGrouped(folderPath);
        foreach (var g in groups)
        {
            FolderGroups.Add(g);
            foreach (var f in g.Files)
            {
                if (!FolderFiles.Any(existing => string.Equals(existing.FullPath, f.FullPath, StringComparison.OrdinalIgnoreCase)))
                {
                    FolderFiles.Add(f);
                }
            }
        }

        ApplyFileFilter();
    }

    public int TotalVisits => Sessions.Count;
    public decimal TotalSpent => Payments.Sum(p => (decimal)p.Amount);
    public string FormattedTotalSpent => $"₹{TotalSpent:N0}";
    public string LastVisitText
    {
        get
        {
            var latest = Sessions.OrderByDescending(s => s.StartedAt).FirstOrDefault();
            return latest != null ? latest.StartedAt.ToLocalTime().ToString("dd MMM yyyy") : "First Visit";
        }
    }
    public bool HasNotes => !string.IsNullOrWhiteSpace(Customer?.Notes);
    public int TotalFileCount => FolderStats.TotalFiles;

    public void NotifyStatsChanged()
    {
        OnPropertyChanged(nameof(TotalVisits));
        OnPropertyChanged(nameof(TotalSpent));
        OnPropertyChanged(nameof(FormattedTotalSpent));
        OnPropertyChanged(nameof(LastVisitText));
        OnPropertyChanged(nameof(HasNotes));
        OnPropertyChanged(nameof(TotalFileCount));
        OnPropertyChanged(nameof(HasPayments));
        OnPropertyChanged(nameof(HasSessions));
        OnPropertyChanged(nameof(HasTimelineItems));
        OnPropertyChanged(nameof(HasFiles));
    }

    [RelayCommand]
    public void OpenCustomerFolder()
    {
        if (Customer == null) return;
        var folderPath = AppServices.FolderManager.GetEffectiveCustomerFolderPath(Customer.Name, Customer.Code, out _);
        AppServices.FolderManager.OpenFolderInExplorer(folderPath);
    }

    [RelayCommand]
    public async Task StartSessionAsync()
    {
        if (Customer == null) return;
        await AppServices.Sessions.StartSessionAsync(Customer.Id);
        ShowSuccess($"Session started for {Customer.Name}");
        
        await LoadCustomerDetailsAsync();
    }

    public async Task<int> ImportFilesAsync()
    {
        if (Customer == null) return 0;
        try
        {
            var files = await AppServices.Pickers.PickMultipleFilesAsync(new[] { "*" });
            if (files == null || files.Count == 0) return 0;

            var folderPath = AppServices.FolderManager.EnsureCustomerWorkingFolder(Customer.Name, Customer.Code);

            int count = 0;
            foreach (var src in files)
            {
                if (File.Exists(src))
                {
                    var baseName = Path.GetFileNameWithoutExtension(src);
                    var ext = Path.GetExtension(src);
                    var uniqueName = SmartTagHelper.GenerateUniqueFileName(folderPath, baseName, ext);
                    var target = Path.Combine(folderPath, uniqueName);
                    File.Copy(src, target, overwrite: false);
                    count++;
                }
            }

            RefreshFiles();
            NotifyStatsChanged();
            ShowSuccess($"Imported {count} file(s) into workspace.");
            return count;
        }
        catch (Exception ex)
        {
            ShowError($"Failed to import files: {ex.Message}");
            return 0;
        }
    }

    public async Task SetCustomerPhotoAsync(string photoPath)
    {
        if (Customer == null) return;
        Customer.PhotoPath = photoPath;
        await AppServices.Customers.UpdatePhotoAsync(Customer.Id, photoPath);
        ShowSuccess("Profile picture updated");
    }

    public async Task RemoveCustomerPhotoAsync()
    {
        if (Customer == null) return;
        Customer.PhotoPath = null;
        await AppServices.Customers.UpdatePhotoAsync(Customer.Id, null);
        ShowSuccess("Profile picture removed");
    }

    public async Task TagAndRenameFileAsync(FolderFileItem item, string tag, Microsoft.UI.Xaml.XamlRoot xamlRoot)
    {
        string? targetTag = tag;
        if (tag == "custom")
        {
            targetTag = await SmartTagHelper.PromptCustomTagAsync(xamlRoot);
            if (string.IsNullOrWhiteSpace(targetTag)) return;
        }

        var dir = Path.GetDirectoryName(item.FullPath) ?? string.Empty;
        var ext = Path.GetExtension(item.FullPath).ToLowerInvariant();
        var uniqueName = SmartTagHelper.GenerateUniqueFileName(dir, targetTag, ext);

        if (AppServices.FolderManager.RenameFile(item.FullPath, uniqueName, out string newFullPath, out string error))
        {
            item.FullPath = newFullPath;
            item.Name = Path.GetFileName(newFullPath);
            item.EditName = item.Name;
            item.Extension = Path.GetExtension(newFullPath).ToLowerInvariant();
            item.IsRenaming = false;

            if (tag == "photo" && item.IsImage)
            {
                await SetCustomerPhotoAsync(newFullPath);
            }
            else
            {
                ShowSuccess($"Renamed to {item.Name}");
            }

            // Refresh grouped view after rename
            RefreshFiles();
        }
        else
        {
            await AppServices.Dialogs.ShowAlertAsync("Tagging Failed", error);
        }
    }
}

