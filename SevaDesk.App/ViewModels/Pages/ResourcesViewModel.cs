using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SevaDesk.Core.Models;

namespace SevaDesk_App.ViewModels.Pages;

public partial class ResourcesViewModel : ObservableObject
{
    [ObservableProperty]
    private ObservableCollection<ResourceItem> _allResources = [];

    [ObservableProperty]
    private ObservableCollection<ResourceItem> _filteredResources = [];

    [ObservableProperty]
    private ResourceItem? _selectedResource;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private string _selectedCategory = "All";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStatusMessage))]
    private string _statusMessage = string.Empty;

    public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);

    public ResourcesViewModel()
    {
        InitializeResources();
    }

    private void InitializeResources()
    {
        AllResources.Clear();

        AllResources.Add(new ResourceItem
        {
            Title = "Form 49A — New PAN Card Physical Form",
            Category = "Blank Forms",
            FileType = "PDF",
            FileSize = "1.2 MB",
            Glyph = "\uE8A5",
            IsFavorite = true
        });

        AllResources.Add(new ResourceItem
        {
            Title = "Income Declaration Affidavit (Standard Format ₹10/₹100 Stamp)",
            Category = "Affidavits",
            FileType = "DOCX",
            FileSize = "45 KB",
            Glyph = "\uE8C1",
            IsFavorite = true
        });

        AllResources.Add(new ResourceItem
        {
            Title = "Educational Gap Year Affidavit (College/Job)",
            Category = "Affidavits",
            FileType = "DOCX",
            FileSize = "38 KB",
            Glyph = "\uE8C1",
            IsFavorite = true
        });

        AllResources.Add(new ResourceItem
        {
            Title = "Name Correction / Alias Affidavit (Govt Gazette)",
            Category = "Affidavits",
            FileType = "DOCX",
            FileSize = "52 KB",
            Glyph = "\uE8C1",
            IsFavorite = false
        });

        AllResources.Add(new ResourceItem
        {
            Title = "Caste Certificate Application Form (State Standard)",
            Category = "Blank Forms",
            FileType = "PDF",
            FileSize = "890 KB",
            Glyph = "\uE8A5",
            IsFavorite = false
        });

        AllResources.Add(new ResourceItem
        {
            Title = "Lost Marksheet / Certificate Police Intimation Format",
            Category = "Affidavits",
            FileType = "DOCX",
            FileSize = "34 KB",
            Glyph = "\uE8C1",
            IsFavorite = false
        });

        AllResources.Add(new ResourceItem
        {
            Title = "SevaDesk Cyber Café Official Rate Card 2026",
            Category = "Guidelines",
            FileType = "PDF",
            FileSize = "240 KB",
            Glyph = "\uE749",
            IsFavorite = true
        });

        AllResources.Add(new ResourceItem
        {
            Title = "Passport Photo Guidelines & Specs Reference",
            Category = "Guidelines",
            FileType = "PDF",
            FileSize = "620 KB",
            Glyph = "\uEB9F",
            IsFavorite = false
        });

        ApplyFilter();
    }

    partial void OnSearchQueryChanged(string value) => ApplyFilter();
    partial void OnSelectedCategoryChanged(string value) => ApplyFilter();

    [RelayCommand]
    public void SetCategory(string category)
    {
        SelectedCategory = category;
    }

    [RelayCommand]
    public void ToggleFavorite(ResourceItem item)
    {
        item.IsFavorite = !item.IsFavorite;
        ApplyFilter();
    }

    [RelayCommand]
    public void QuickPrint(ResourceItem item)
    {
        StatusMessage = $"Sent '{item.Title}' to default printer.";
    }

    [RelayCommand]
    public void CopyToActiveSession(ResourceItem item)
    {
        StatusMessage = $"Copied '{item.Title}' to Active Customer's 01_Shared Documents folder.";
    }

    private void ApplyFilter()
    {
        var filtered = AllResources.AsEnumerable();

        if (SelectedCategory != "All")
        {
            filtered = filtered.Where(r => r.Category == SelectedCategory);
        }

        if (!string.IsNullOrWhiteSpace(SearchQuery))
        {
            filtered = filtered.Where(r => r.Title.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) ||
                                           r.Category.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase));
        }

        FilteredResources = new ObservableCollection<ResourceItem>(filtered);
        SelectedResource = FilteredResources.FirstOrDefault();
    }
}
