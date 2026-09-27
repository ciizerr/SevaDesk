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
using Windows.System;

namespace SevaDesk_App.ViewModels.Pages;

public partial class ApplicationsViewModel : StatusViewModel
{
    // --- Top View Switcher ---
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsApplicationsTab))]
    [NotifyPropertyChangedFor(nameof(IsTemplatesTab))]
    [NotifyPropertyChangedFor(nameof(IsCatalogTab))]
    [NotifyPropertyChangedFor(nameof(IsSubmissionsTab))]
    private int _selectedTabIndex = 0; // 0 = Customer Applications (Default), 1 = Form Templates

    public bool IsApplicationsTab => SelectedTabIndex == 0;
    public bool IsTemplatesTab => SelectedTabIndex == 1;

    // Backward compatibility aliases
    public bool IsCatalogTab => IsTemplatesTab;
    public bool IsSubmissionsTab => IsApplicationsTab;

    // --- Tab 0: Customer Applications ---
    private readonly List<ApplicationItem> _allApplications = [];

    [ObservableProperty]
    private ObservableCollection<ApplicationItem> _applications = [];

    [ObservableProperty]
    private ObservableCollection<ApplicationChecklistItem> _selectedChecklist = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedApplication))]
    private ApplicationItem? _selectedApplication;

    public bool HasSelectedApplication => SelectedApplication != null;

    [ObservableProperty]
    private string _filterStatus = "All";

    [ObservableProperty]
    private string _applicationSearchQuery = string.Empty;

    // Status Count Badges
    [ObservableProperty] private int _allApplicationsCount;
    [ObservableProperty] private int _draftCount;
    [ObservableProperty] private int _docsReadyCount;
    [ObservableProperty] private int _completedCount;

    // Document Checklist Progress for Selected Application
    public int ChecklistTotalCount => SelectedChecklist.Count;
    public int ChecklistCompletedCount => SelectedChecklist.Count(i => i.IsCompleted || i.HasMatchedFile);
    public double ChecklistProgressPercent => ChecklistTotalCount > 0 ? (double)ChecklistCompletedCount / ChecklistTotalCount * 100.0 : 0.0;
    public string ChecklistProgressText => ChecklistTotalCount > 0
        ? $"{ChecklistCompletedCount} of {ChecklistTotalCount} documents verified ({(int)ChecklistProgressPercent}%)"
        : "No documents required";

    // --- Tab 1: Form Templates ---
    [ObservableProperty]
    private ObservableCollection<ApplicationTemplate> _templates = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedTemplate))]
    private ApplicationTemplate? _selectedTemplate;

    public bool HasSelectedTemplate => SelectedTemplate != null;

    [ObservableProperty]
    private string _categoryFilter = "All";

    [ObservableProperty]
    private string _templateSearchQuery = string.Empty;

    // --- Active Sessions for Quick Linking ---
    [ObservableProperty]
    private ObservableCollection<ActiveSessionItem> _activeSessions = [];

    public ApplicationsViewModel()
    {
        _ = LoadApplicationsAsync();
        _ = LoadTemplatesAsync();
        _ = RefreshActiveSessionsAsync();
    }

    // --- Customer Applications Commands ---
    [RelayCommand]
    public async Task LoadApplicationsAsync()
    {
        var items = (await AppServices.Applications.GetAllAsync(null)).ToList();
        _allApplications.Clear();
        _allApplications.AddRange(items);

        UpdateStatusCounts();
        ApplyApplicationFilters();

        if (SelectedApplication == null || !Applications.Contains(SelectedApplication))
        {
            SelectedApplication = Applications.FirstOrDefault();
        }
        _ = RefreshChecklistForSelectedAsync();
    }

    public void ApplyApplicationFilters()
    {
        var filtered = _allApplications.AsEnumerable();

        if (FilterStatus != "All")
        {
            if (FilterStatus == "Docs Ready")
            {
                filtered = filtered.Where(a => a.Status == "Docs Ready" || a.Status == "Docs Uploaded");
            }
            else
            {
                filtered = filtered.Where(a => string.Equals(a.Status, FilterStatus, StringComparison.OrdinalIgnoreCase));
            }
        }

        if (!string.IsNullOrWhiteSpace(ApplicationSearchQuery))
        {
            var q = ApplicationSearchQuery.Trim();
            filtered = filtered.Where(a =>
                (!string.IsNullOrWhiteSpace(a.Title) && a.Title.Contains(q, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrWhiteSpace(a.CustomerName) && a.CustomerName.Contains(q, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrWhiteSpace(a.ApplicationNumber) && a.ApplicationNumber.Contains(q, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrWhiteSpace(a.PortalName) && a.PortalName.Contains(q, StringComparison.OrdinalIgnoreCase)));
        }

        var selectedId = SelectedApplication?.Id;
        Applications.Clear();
        foreach (var app in filtered)
        {
            Applications.Add(app);
        }

        if (selectedId != null)
        {
            SelectedApplication = Applications.FirstOrDefault(a => a.Id == selectedId) ?? Applications.FirstOrDefault();
        }
        else
        {
            SelectedApplication = Applications.FirstOrDefault();
        }
    }

    private void UpdateStatusCounts()
    {
        AllApplicationsCount = _allApplications.Count;
        DraftCount = _allApplications.Count(a => a.Status == "Draft");
        DocsReadyCount = _allApplications.Count(a => a.Status == "Docs Ready" || a.Status == "Docs Uploaded");
        CompletedCount = _allApplications.Count(a => a.Status == "Completed");
    }

    partial void OnFilterStatusChanged(string value)
    {
        ApplyApplicationFilters();
    }

    partial void OnApplicationSearchQueryChanged(string value)
    {
        ApplyApplicationFilters();
    }

    partial void OnSelectedApplicationChanged(ApplicationItem? value)
    {
        _ = RefreshChecklistForSelectedAsync();
    }

    public async Task RefreshChecklistForSelectedAsync()
    {
        SelectedChecklist.Clear();
        if (SelectedApplication != null && !string.IsNullOrWhiteSpace(SelectedApplication.RequiredDocs))
        {
            var result = await ApplicationDocumentVerifier.CheckAndAutoUpdateStatusAsync(SelectedApplication);
            foreach (var doc in result.Checklist)
            {
                SelectedChecklist.Add(doc);
            }
        }

        NotifyChecklistStatsChanged();
        OnPropertyChanged(nameof(SelectedApplication));
    }

    public void NotifyChecklistStatsChanged()
    {
        OnPropertyChanged(nameof(ChecklistTotalCount));
        OnPropertyChanged(nameof(ChecklistCompletedCount));
        OnPropertyChanged(nameof(ChecklistProgressPercent));
        OnPropertyChanged(nameof(ChecklistProgressText));
    }

    public async Task OnChecklistItemToggledAsync()
    {
        if (SelectedApplication == null) return;
        bool allChecked = SelectedChecklist.Count > 0 && SelectedChecklist.All(i => i.IsCompleted || i.HasMatchedFile);
        if (allChecked && SelectedApplication.Status == "Draft")
        {
            SelectedApplication.Status = "Docs Ready";
            SelectedApplication.UpdatedAt = DateTime.UtcNow;
            await AppServices.Applications.UpdateAsync(SelectedApplication);
            UpdateStatusCounts();
            OnPropertyChanged(nameof(SelectedApplication));
        }
        else if (!allChecked && SelectedApplication.Status == "Docs Ready")
        {
            SelectedApplication.Status = "Draft";
            SelectedApplication.UpdatedAt = DateTime.UtcNow;
            await AppServices.Applications.UpdateAsync(SelectedApplication);
            UpdateStatusCounts();
            OnPropertyChanged(nameof(SelectedApplication));
        }
        NotifyChecklistStatsChanged();
    }

    [RelayCommand]
    public async Task UpdateApplicationStatusAsync(string newStatus)
    {
        if (SelectedApplication == null) return;
        SelectedApplication.Status = newStatus;
        SelectedApplication.UpdatedAt = DateTime.UtcNow;
        await AppServices.Applications.UpdateAsync(SelectedApplication);
        UpdateStatusCounts();
        ShowSuccess($"Status updated to '{newStatus}' for {SelectedApplication.Title}.");
        _ = RefreshChecklistForSelectedAsync();
        OnPropertyChanged(nameof(Applications));
    }

    [RelayCommand]
    public async Task DeleteApplicationAsync()
    {
        if (SelectedApplication == null) return;
        var title = SelectedApplication.Title;
        await AppServices.Applications.DeleteAsync(SelectedApplication.Id);
        _allApplications.Remove(SelectedApplication);
        Applications.Remove(SelectedApplication);
        SelectedApplication = Applications.FirstOrDefault();
        UpdateStatusCounts();
        ShowInfo($"Application '{title}' deleted.");
    }

    [RelayCommand]
    public async Task OpenCustomerFolderAsync()
    {
        if (SelectedApplication == null || string.IsNullOrWhiteSpace(SelectedApplication.CustomerName)) return;

        var folderPath = AppServices.FolderManager.GetCustomerFolderPath(SelectedApplication.CustomerName, "");
        if (Directory.Exists(folderPath))
        {
            AppServices.FolderManager.OpenFileWithDefaultApp(folderPath);
            return;
        }

        if (!string.IsNullOrWhiteSpace(SelectedApplication.CustomerId))
        {
            var cust = await AppServices.Customers.GetByIdAsync(SelectedApplication.CustomerId);
            if (cust != null)
            {
                var p = AppServices.FolderManager.GetCustomerFolderPath(cust.Name, cust.Code);
                if (Directory.Exists(p))
                {
                    AppServices.FolderManager.OpenFileWithDefaultApp(p);
                    return;
                }
            }
        }

        ShowWarning($"Working folder for customer '{SelectedApplication.CustomerName}' not found.");
    }

    [RelayCommand]
    public async Task OpenPortalUrl(string portal)
    {
        if (string.IsNullOrWhiteSpace(portal)) return;
        var url = portal.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? portal : $"https://{portal}";
        try
        {
            await Launcher.LaunchUriAsync(new Uri(url));
        }
        catch
        {
            ShowError($"Failed to open website: {url}");
        }
    }

    [RelayCommand]
    public async Task AddApplicationAsync(ApplicationItem newApp)
    {
        if (newApp == null) return;
        var created = await AppServices.Applications.CreateAsync(newApp);
        _allApplications.Insert(0, created);
        Applications.Insert(0, created);
        SelectedApplication = created;
        UpdateStatusCounts();
        ShowSuccess($"Application '{created.Title}' registered.");
    }

    // --- Form Templates Commands ---
    [RelayCommand]
    public async Task LoadTemplatesAsync()
    {
        Templates.Clear();
        var cat = CategoryFilter == "All" ? null : CategoryFilter;
        var items = await AppServices.Applications.GetAllTemplatesAsync(cat);

        if (!string.IsNullOrWhiteSpace(TemplateSearchQuery))
        {
            items = items.Where(t =>
                t.Title.Contains(TemplateSearchQuery, StringComparison.OrdinalIgnoreCase) ||
                t.Category.Contains(TemplateSearchQuery, StringComparison.OrdinalIgnoreCase) ||
                (t.Notes != null && t.Notes.Contains(TemplateSearchQuery, StringComparison.OrdinalIgnoreCase)));
        }

        foreach (var itm in items)
        {
            Templates.Add(itm);
        }

        if (SelectedTemplate == null || !Templates.Contains(SelectedTemplate))
        {
            SelectedTemplate = Templates.FirstOrDefault();
        }
    }

    [RelayCommand]
    public void CreateNewTemplate()
    {
        var newTmpl = new ApplicationTemplate
        {
            Title = "New Form / Scheme",
            Category = "Jobs & Exams",
            PortalUrl = "https://",
            DefaultServiceFee = 100,
            DefaultGovtFee = 0,
            RequiredDocs = "Photo, Signature, Aadhaar",
            Notes = "Enter important instructions or last date here"
        };
        Templates.Insert(0, newTmpl);
        SelectedTemplate = newTmpl;
        ShowInfo("New form template created. Fill in details and click Save.");
    }

    [RelayCommand]
    public async Task SaveTemplateAsync()
    {
        if (SelectedTemplate == null) return;
        if (string.IsNullOrWhiteSpace(SelectedTemplate.Title))
        {
            ShowWarning("Form title cannot be empty.");
            return;
        }

        var existing = await AppServices.Applications.GetTemplateByIdAsync(SelectedTemplate.Id);
        if (existing == null)
        {
            await AppServices.Applications.CreateTemplateAsync(SelectedTemplate);
            ShowSuccess($"Template '{SelectedTemplate.Title}' saved.");
        }
        else
        {
            await AppServices.Applications.UpdateTemplateAsync(SelectedTemplate);
            ShowSuccess($"Template '{SelectedTemplate.Title}' updated.");
        }
        OnPropertyChanged(nameof(Templates));
    }

    [RelayCommand]
    public async Task DeleteTemplateAsync()
    {
        if (SelectedTemplate == null) return;
        var title = SelectedTemplate.Title;
        await AppServices.Applications.DeleteTemplateAsync(SelectedTemplate.Id);
        Templates.Remove(SelectedTemplate);
        SelectedTemplate = Templates.FirstOrDefault();
        ShowInfo($"Template '{title}' deleted.");
    }

    // --- Quick Toggle Common Document Chip in Template ---
    public void ToggleTemplateRequiredDocument(string docName)
    {
        if (SelectedTemplate == null || string.IsNullOrWhiteSpace(docName)) return;

        var current = (SelectedTemplate.RequiredDocs ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        var existingMatch = current.FirstOrDefault(d => string.Equals(d, docName, StringComparison.OrdinalIgnoreCase));
        if (existingMatch != null)
        {
            current.Remove(existingMatch);
        }
        else
        {
            current.Add(docName);
        }

        SelectedTemplate.RequiredDocs = string.Join(", ", current);
        OnPropertyChanged(nameof(SelectedTemplate));
    }

    public bool IsDocumentInSelectedTemplate(string docName)
    {
        if (SelectedTemplate == null || string.IsNullOrWhiteSpace(SelectedTemplate.RequiredDocs)) return false;
        var current = SelectedTemplate.RequiredDocs
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return current.Any(d => string.Equals(d, docName, StringComparison.OrdinalIgnoreCase));
    }

    // --- Active Sessions for "Start for Customer" ---
    public async Task RefreshActiveSessionsAsync()
    {
        ActiveSessions.Clear();
        var sessions = await AppServices.Sessions.GetActiveSessionsAsync();
        foreach (var s in sessions)
        {
            ActiveSessions.Add(s);
        }
    }

    public async Task LinkTemplateToSessionAsync(ActiveSessionItem session, ApplicationTemplate template)
    {
        if (session == null || template == null) return;

        var app = new ApplicationItem
        {
            CustomerId = session.Customer.Id,
            CustomerName = session.Customer.Name,
            SessionId = session.Session.Id,
            Title = template.Title,
            PortalName = template.PortalUrl,
            ServiceCharge = template.DefaultServiceFee,
            GovtFee = template.DefaultGovtFee,
            RequiredDocs = template.RequiredDocs,
            Status = "Draft"
        };

        var created = await AppServices.Applications.CreateAsync(app);
        session.LinkedApplication = created;
        session.NotifyLinkedApplicationChanged();

        var folderSafe = string.Join("_", template.Title.Split(Path.GetInvalidFileNameChars()));
        AppServices.FolderManager.EnsureApplicationSubfolder(session.FolderPath, folderSafe);

        await LoadApplicationsAsync();
        SelectedApplication = Applications.FirstOrDefault(a => a.Id == created.Id);
        SelectedTabIndex = 0; // Switch to Customer Applications tab!
        ShowSuccess($"Linked '{template.Title}' to {session.Customer.Name}.");
    }
}
