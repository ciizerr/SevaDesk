using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SevaDesk.Core.Models;
using SevaDesk_App.Services;
using Windows.System;

namespace SevaDesk_App.ViewModels.Pages;

public partial class ApplicationsViewModel : ObservableObject
{
    // --- Top View Switcher ---
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCatalogTab))]
    [NotifyPropertyChangedFor(nameof(IsSubmissionsTab))]
    private int _selectedTabIndex = 0; // 0 = Scheme & Form Catalog, 1 = Citizen Submissions Ledger

    public bool IsCatalogTab => SelectedTabIndex == 0;
    public bool IsSubmissionsTab => SelectedTabIndex == 1;

    // --- Tab 1: Scheme & Form Templates Catalog ---
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

    // --- Tab 2: Citizen Submissions Ledger ---
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
    [NotifyPropertyChangedFor(nameof(HasStatusMessage))]
    private string _statusMessage = string.Empty;

    public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);

    public ApplicationsViewModel()
    {
        _ = LoadTemplatesAsync();
        _ = LoadApplicationsAsync();
    }

    // --- Template Commands ---
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

        SelectedTemplate = Templates.FirstOrDefault();
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
        StatusMessage = "Draft template created. Fill in details and click Save.";
    }

    [RelayCommand]
    public async Task SaveTemplateAsync()
    {
        if (SelectedTemplate == null) return;
        if (string.IsNullOrWhiteSpace(SelectedTemplate.Title))
        {
            StatusMessage = "Template title cannot be empty.";
            return;
        }

        var existing = await AppServices.Applications.GetTemplateByIdAsync(SelectedTemplate.Id);
        if (existing == null)
        {
            await AppServices.Applications.CreateTemplateAsync(SelectedTemplate);
            StatusMessage = $"Template '{SelectedTemplate.Title}' saved to catalog.";
        }
        else
        {
            await AppServices.Applications.UpdateTemplateAsync(SelectedTemplate);
            StatusMessage = $"Template '{SelectedTemplate.Title}' updated.";
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
        StatusMessage = $"Template '{title}' deleted from catalog.";
    }

    // --- Submissions Commands ---
    [RelayCommand]
    public async Task LoadApplicationsAsync()
    {
        Applications.Clear();
        var statusFilter = FilterStatus == "All" ? null : FilterStatus;
        var items = await AppServices.Applications.GetAllAsync(statusFilter);
        foreach (var item in items)
        {
            Applications.Add(item);
        }

        SelectedApplication = Applications.FirstOrDefault();
        UpdateChecklistForSelected();
    }

    partial void OnSelectedApplicationChanged(ApplicationItem? value)
    {
        UpdateChecklistForSelected();
    }

    private void UpdateChecklistForSelected()
    {
        SelectedChecklist.Clear();
        if (SelectedApplication == null || string.IsNullOrWhiteSpace(SelectedApplication.RequiredDocs)) return;

        var docs = SelectedApplication.RequiredDocs.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        foreach (var doc in docs)
        {
            SelectedChecklist.Add(new ApplicationChecklistItem
            {
                Title = doc,
                IsCompleted = SelectedApplication.Status != "Draft",
                Category = "Document"
            });
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
        catch
        {
            StatusMessage = $"Failed to launch {url}";
        }
    }

    [RelayCommand]
    public async Task UpdateApplicationStatusAsync(string newStatus)
    {
        if (SelectedApplication == null) return;
        SelectedApplication.Status = newStatus;
        SelectedApplication.UpdatedAt = DateTime.UtcNow;
        await AppServices.Applications.UpdateAsync(SelectedApplication);
        StatusMessage = $"Status updated to '{newStatus}' for {SelectedApplication.Title}.";
        UpdateChecklistForSelected();
        OnPropertyChanged(nameof(Applications));
    }

    [RelayCommand]
    public async Task AddApplicationAsync(ApplicationItem newApp)
    {
        if (newApp == null) return;
        var created = await AppServices.Applications.CreateAsync(newApp);
        Applications.Insert(0, created);
        SelectedApplication = created;
        StatusMessage = $"Application '{created.Title}' registered.";
    }
}
