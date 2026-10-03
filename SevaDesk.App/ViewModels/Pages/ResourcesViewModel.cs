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
    public void OpenDocument(ResourceItem? item)
    {
        var target = item ?? SelectedResource;
        if (target == null) return;

        if (!target.HasFile)
        {
            ShowWarning($"No document file is attached yet for \"{target.Title}\". Click \"Attach File\" to link a PDF or Word document.");
            return;
        }

        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = target.FilePath!,
                UseShellExecute = true
            };
            System.Diagnostics.Process.Start(psi);
            ShowSuccess($"Opened \"{target.Title}\" in default viewer.");
        }
        catch (Exception ex)
        {
            ShowError($"Could not open document: {ex.Message}");
        }
    }

    [RelayCommand]
    public void OpenFileLocation(ResourceItem? item)
    {
        var target = item ?? SelectedResource;
        if (target == null) return;

        if (!target.HasFile)
        {
            ShowWarning($"No document file is linked yet for \"{target.Title}\". Click \"Attach File\" to link a file.");
            return;
        }

        try
        {
            var fullPath = Path.GetFullPath(target.FilePath!);
            System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{fullPath}\"");
        }
        catch (Exception ex)
        {
            ShowError($"Could not open folder location: {ex.Message}");
        }
    }

    [RelayCommand]
    public void QuickPrint(ResourceItem? item)
    {
        var target = item ?? SelectedResource;
        if (target == null) return;

        if (!target.HasFile)
        {
            ShowWarning($"No document file is attached to print for \"{target.Title}\". Click \"Attach File\" to link a document.");
            return;
        }

        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = target.FilePath!,
                Verb = "print",
                CreateNoWindow = true,
                WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden,
                UseShellExecute = true
            };
            System.Diagnostics.Process.Start(psi);
            ShowSuccess($"Sent \"{target.Title}\" to default counter printer.");
        }
        catch
        {
            // If direct print verb is not registered for the file type, open default app for printing
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(target.FilePath!) { UseShellExecute = true });
                ShowSuccess($"Opened \"{target.Title}\" for printing.");
            }
            catch (Exception ex)
            {
                ShowError($"Failed to print: {ex.Message}");
            }
        }
    }

    [RelayCommand]
    public async Task SaveSelectedResourceAsync()
    {
        if (SelectedResource == null) return;

        try
        {
            await AppServices.Resources.UpdateResourceAsync(SelectedResource);
            ShowSuccess($"Changes for \"{SelectedResource.Title}\" saved successfully.");
        }
        catch (Exception ex)
        {
            ShowError($"Failed to save template details: {ex.Message}");
        }
    }

    public async Task AttachFileToResourceAsync(ResourceItem item, string sourceFilePath)
    {
        if (item == null || string.IsNullOrWhiteSpace(sourceFilePath)) return;

        try
        {
            var targetPath = SevaDesk.Infrastructure.FileManager.TemplateStorageHelper.StoreTemplateFile(
                sourceFilePath, item.Category, item.Title);

            var fi = new FileInfo(targetPath);
            item.FilePath = targetPath;
            item.FileSize = SevaDesk.Infrastructure.FileManager.TemplateStorageHelper.FormatFileSize(fi.Length);
            item.FileType = Path.GetExtension(targetPath).TrimStart('.').ToUpperInvariant();
            item.Glyph = item.FileType == "PDF" ? "\uE8A5" : "\uE8C1";

            await AppServices.Resources.UpdateResourceAsync(item);
            ShowSuccess($"Document file linked to \"{item.Title}\" successfully.");
        }
        catch (Exception ex)
        {
            ShowError($"Could not attach document: {ex.Message}");
        }
    }

    [RelayCommand]
    public async Task CopyToActiveSessionAsync(ResourceItem item)
    {
        if (item == null) return;

        if (!item.HasFile)
        {
            ShowWarning($"No document file is attached yet for \"{item.Title}\". Attach a file first before copying to session.");
            return;
        }

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
            var ext = Path.GetExtension(item.FilePath);
            if (string.IsNullOrWhiteSpace(ext))
            {
                ext = item.FileType.ToLowerInvariant() == "pdf" ? ".pdf" : ".docx";
            }
            var destPath = Path.Combine(destFolder, $"{safeTitle}{ext}");

            File.Copy(item.FilePath!, destPath, overwrite: true);
            ShowSuccess($"Copied \"{item.Title}\" to {current.Customer.Name}'s Shared Docs folder.");
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
        SelectedResource = created;
        ShowSuccess($"Added \"{created.Title}\" to template catalog.");
    }

    [RelayCommand]
    public async Task DeleteResourceAsync(ResourceItem? item)
    {
        var target = item ?? SelectedResource;
        if (target == null) return;

        try
        {
            // 1. Delete from database
            await AppServices.Resources.DeleteAsync(target.Id);

            // 2. Permanently delete physical file if exists on disk
            if (!string.IsNullOrWhiteSpace(target.FilePath) && File.Exists(target.FilePath))
            {
                try
                {
                    File.Delete(target.FilePath);
                }
                catch (Exception ex)
                {
                    ShowWarning($"Template record deleted, but could not delete physical file: {ex.Message}");
                }
            }

            // 3. Remove from collections & refresh filter
            var existing = AllResources.FirstOrDefault(r => r.Id == target.Id) ?? target;
            AllResources.Remove(existing);
            ApplyFilter();

            ShowSuccess($"Deleted \"{target.Title}\" successfully.");
        }
        catch (Exception ex)
        {
            ShowError($"Failed to delete template: {ex.Message}");
        }
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

        FilteredResources.Clear();
        foreach (var item in filtered)
        {
            FilteredResources.Add(item);
        }
        if (SelectedResource == null || !FilteredResources.Contains(SelectedResource))
        {
            SelectedResource = FilteredResources.FirstOrDefault();
        }
    }
}

