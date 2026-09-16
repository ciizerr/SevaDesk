using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SevaDesk.Core.Models;
using SevaDesk_App.Services;

namespace SevaDesk_App.ViewModels.Pages;

public partial class ResourcesViewModel : StatusViewModel
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


    public ResourcesViewModel()
    {
        _ = LoadResourcesAsync();
    }

    [RelayCommand]
    public async Task LoadResourcesAsync()
    {
        AllResources.Clear();
        var items = await AppServices.Resources.GetAllAsync();
        foreach (var item in items)
        {
            AllResources.Add(item);
        }

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
    public async Task ToggleFavoriteAsync(ResourceItem item)
    {
        if (item == null) return;
        item.IsFavorite = !item.IsFavorite;
        await AppServices.Resources.ToggleFavoriteAsync(item.Id, item.IsFavorite);
        ApplyFilter();
    }

    [RelayCommand]
    public void QuickPrint(ResourceItem item)
    {
        if (item == null) return;
        ShowSuccess($"Sent '{item.Title}' to default printer queue.");
    }

    [RelayCommand]
    public async Task CopyToActiveSessionAsync(ResourceItem item)
    {
        if (item == null) return;

        var activeSessions = (await AppServices.Sessions.GetActiveSessionsAsync()).ToList();
        var current = activeSessions.FirstOrDefault();
        if (current == null)
        {
            ShowWarning("No active customer session. Start a customer session first to copy resources.");
            return;
        }

        try
        {
            var destFolder = Path.Combine(current.FolderPath, "Shared Docs");
            Directory.CreateDirectory(destFolder);

            var safeTitle = string.Join("_", item.Title.Split(Path.GetInvalidFileNameChars()));
            var ext = item.FileType.ToLowerInvariant() == "pdf" ? ".pdf" : ".docx";
            var destPath = Path.Combine(destFolder, $"{safeTitle}{ext}");

            if (item.FilePath != null && File.Exists(item.FilePath))
            {
                File.Copy(item.FilePath, destPath, overwrite: true);
            }
            else
            {
                File.WriteAllText(destPath, $"[SevaDesk Resource Template: {item.Title}]\nCategory: {item.Category}\nCreated: {DateTime.Now}\n");
            }

            ShowSuccess($"Copied '{item.Title}' to {current.Customer.Name}'s Shared Docs folder.");
        }
        catch (Exception ex)
        {
            ShowError($"Failed to copy template: {ex.Message}");
        }
    }

    [RelayCommand]
    public async Task AddResourceAsync(ResourceItem item)
    {
        if (item == null) return;
        var created = await AppServices.Resources.AddCustomResourceAsync(item);
        AllResources.Insert(0, created);
        ApplyFilter();
        ShowSuccess($"Added resource '{created.Title}' to catalog.");
    }

    private void ApplyFilter()
    {
        var filtered = AllResources.AsEnumerable();

        if (SelectedCategory != "All")
        {
            filtered = filtered.Where(r => r.Category.Equals(SelectedCategory, StringComparison.OrdinalIgnoreCase));
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

