using System;
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
    private ObservableCollection<FolderFileItem> _folderFiles = [];

    [ObservableProperty]
    private ObservableCollection<FolderGroup> _folderGroups = [];

    [ObservableProperty]
    private FolderStats _folderStats = new();

    [ObservableProperty]
    private bool _isShowingBackup;

    public CustomerWorkspaceViewModel()
    {
    }

    public async Task InitializeAsync(Customer customer)
    {
        Customer = customer;
        await LoadCustomerDetailsAsync();
    }

    [RelayCommand]
    private async Task LoadCustomerDetailsAsync()
    {
        if (Customer == null) return;

        Sessions.Clear();
        Payments.Clear();
        FolderFiles.Clear();
        FolderGroups.Clear();
        FolderStats = new FolderStats();

        IsLoading = true;
        try
        {
            // Load Sessions
            var sessions = await AppServices.Sessions.GetCustomerSessionsAsync(Customer.Id);
            foreach (var s in sessions) Sessions.Add(s);

            // Load Payments
            var payments = await AppServices.Payments.GetCustomerPaymentsAsync(Customer.Id);
            foreach (var p in payments) Payments.Add(p);

            // Correlate sessions with payments for rich history display
            SessionHistory.Clear();
            foreach (var s in sessions.OrderByDescending(x => x.StartedAt))
            {
                var matchingPayment = payments.FirstOrDefault(p => p.SessionId == s.Id);
                SessionHistory.Add(new CustomerSessionRowModel(s, matchingPayment));
            }

            // Load Files (both flat and grouped)
            RefreshFiles();

            NotifyStatsChanged();
        }
        finally
        {
            IsLoading = false;
        }
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
    }

    public int TotalVisits => Sessions.Count;
    public decimal TotalSpent => Payments.Sum(p => (decimal)p.Amount);
    public string FormattedTotalSpent => $"₹{TotalSpent:N0}";
    public string LastVisitText
    {
        get
        {
            var latest = Sessions.OrderByDescending(s => s.StartedAt).FirstOrDefault();
            return latest != null ? latest.StartedAt.ToString("dd MMM yyyy") : "First Visit";
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
        
        // Refresh sessions list and files
        Sessions.Clear();
        var sessions = await AppServices.Sessions.GetCustomerSessionsAsync(Customer.Id);
        foreach (var s in sessions) Sessions.Add(s);

        SessionHistory.Clear();
        foreach (var s in sessions.OrderByDescending(x => x.StartedAt))
        {
            var matchingPayment = Payments.FirstOrDefault(p => p.SessionId == s.Id);
            SessionHistory.Add(new CustomerSessionRowModel(s, matchingPayment));
        }

        RefreshFiles();
        NotifyStatsChanged();
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
