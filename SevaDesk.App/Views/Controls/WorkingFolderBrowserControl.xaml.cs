using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using SevaDesk.Core.Models;
using SevaDesk_App.Services;
using SevaDesk_App.Views.Dialogs;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace SevaDesk_App.Views.Controls;

public sealed partial class WorkingFolderBrowserControl : UserControl
{
    public ObservableCollection<FolderFileItem> Files { get; } = new();
    public ObservableCollection<SubfolderItem> AppSubfolders { get; } = new();

    public string CustomerFolderPath { get; private set; } = string.Empty;
    public string SelectedSubfolderRelative { get; private set; } = string.Empty;
    public Customer? CurrentCustomer { get; private set; }
    public event Action<string>? CustomerPhotoUpdated;
    public event Action? FilesChanged;

    public string CurrentSubfolderFullPath => string.IsNullOrEmpty(SelectedSubfolderRelative)
        ? CustomerFolderPath
        : Path.Combine(CustomerFolderPath, SelectedSubfolderRelative);

    private FileSystemWatcher? _watcher;
    private DispatcherTimer? _watcherDebounceTimer;

    public static Visibility VisibleIf(bool condition) => condition ? Visibility.Visible : Visibility.Collapsed;
    public static Visibility CollapsedIf(bool condition) => condition ? Visibility.Collapsed : Visibility.Visible;

    public WorkingFolderBrowserControl()
    {
        InitializeComponent();
        FilesListView.ItemsSource = Files;
        AppSubfoldersControl.ItemsSource = AppSubfolders;
        Unloaded += (s, e) => CleanupWatcher();
    }

    private void CleanupWatcher()
    {
        try
        {
            _watcher?.Dispose();
            _watcher = null;
            _watcherDebounceTimer?.Stop();
        }
        catch { }
    }

    private void SetupWatcher(string folderPath)
    {
        CleanupWatcher();
        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath)) return;

        try
        {
            _watcherDebounceTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(300)
            };
            _watcherDebounceTimer.Tick += (s, e) =>
            {
                _watcherDebounceTimer.Stop();
                // If user is currently editing a filename, delay refreshing so we don't interrupt typing
                if (Files.Any(f => f.IsRenaming)) return;

                RefreshSubfolderStrip();
                LoadFilesForCurrentSubfolder();
                CheckAndAutoDetectCustomerPhoto();
                FilesChanged?.Invoke();
            };


            _watcher = new FileSystemWatcher(folderPath)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite | NotifyFilters.Size,
                EnableRaisingEvents = true
            };

            _watcher.Created += (s, e) => TriggerWatcherDebounce();
            _watcher.Deleted += (s, e) => TriggerWatcherDebounce();
            _watcher.Renamed += (s, e) => TriggerWatcherDebounce();
            _watcher.Changed += (s, e) => TriggerWatcherDebounce();
        }
        catch { }
    }

    private void TriggerWatcherDebounce()
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            _watcherDebounceTimer?.Stop();
            _watcherDebounceTimer?.Start();
        });
    }

    private void CheckAndAutoDetectCustomerPhoto()
    {
        if (CurrentCustomer == null || string.IsNullOrWhiteSpace(CustomerFolderPath) || !Directory.Exists(CustomerFolderPath))
            return;

        try
        {
            var match = Directory.GetFiles(CustomerFolderPath, "*.*", SearchOption.AllDirectories)
                .FirstOrDefault(f => SmartTagHelper.IsImageFile(f) && SmartTagHelper.IsPhotoTag(Path.GetFileNameWithoutExtension(f)));

            if (!string.IsNullOrWhiteSpace(match) && !string.Equals(CurrentCustomer.PhotoPath, match, StringComparison.OrdinalIgnoreCase))
            {
                CurrentCustomer.PhotoPath = match;
                _ = AppServices.Customers.UpdatePhotoAsync(CurrentCustomer.Id, match);
                CustomerAvatarHelper.NotifyAvatarUpdated(match);
                CustomerPhotoUpdated?.Invoke(match);
            }
        }
        catch { }
    }


    public void LoadCustomerFolder(string folderPath, Customer? customer = null)
    {
        CurrentCustomer = customer;
        CustomerFolderPath = folderPath ?? string.Empty;
        if (string.IsNullOrWhiteSpace(CustomerFolderPath) || !Directory.Exists(CustomerFolderPath))
        {
            CleanupWatcher();
            Files.Clear();
            AppSubfolders.Clear();
            TxtUnorganisedBadge.Text = "0";
            TxtSharedBadge.Text = "0";
            UpdateEmptyState();
            return;
        }

        SetupWatcher(CustomerFolderPath);
        RefreshSubfolderStrip();
        SelectSubfolder(string.Empty);
        CheckAndAutoDetectCustomerPhoto();
    }



    public void RefreshSubfolderStrip()
    {
        if (string.IsNullOrWhiteSpace(CustomerFolderPath) || !Directory.Exists(CustomerFolderPath)) return;

        // 1. Unorganised Files count (top directory files + legacy 00_Unorganised if present)
        try
        {
            var unorganisedCount = Directory.GetFiles(CustomerFolderPath, "*", SearchOption.TopDirectoryOnly).Length;
            var unorganisedSubPath = Path.Combine(CustomerFolderPath, "00_Unorganised");
            if (Directory.Exists(unorganisedSubPath))
            {
                unorganisedCount += Directory.GetFiles(unorganisedSubPath, "*", SearchOption.TopDirectoryOnly).Length;
            }
            TxtUnorganisedBadge.Text = unorganisedCount.ToString();
        }
        catch
        {
            TxtUnorganisedBadge.Text = "0";
        }

        // 2. Shared Docs count
        var sharedPath = Path.Combine(CustomerFolderPath, "Shared Docs");
        try
        {
            var sharedCount = Directory.Exists(sharedPath)
                ? Directory.GetFiles(sharedPath, "*", SearchOption.TopDirectoryOnly).Length
                : 0;
            TxtSharedBadge.Text = sharedCount.ToString();
        }
        catch
        {
            TxtSharedBadge.Text = "0";
        }

        // 3. Application subfolders
        AppSubfolders.Clear();
        foreach (var sub in AppServices.FolderManager.GetApplicationSubfolders(CustomerFolderPath))
        {
            var fullSubPath = Path.Combine(CustomerFolderPath, sub);
            int count = 0;
            try
            {
                if (Directory.Exists(fullSubPath))
                {
                    count = Directory.GetFiles(fullSubPath, "*", SearchOption.TopDirectoryOnly).Length;
                }
            }
            catch { }

            AppSubfolders.Add(new SubfolderItem
            {
                DisplayName = sub,
                RelativePath = sub,
                FullPath = fullSubPath,
                FileCount = count,
                IsSelected = string.Equals(SelectedSubfolderRelative, sub, StringComparison.OrdinalIgnoreCase)
            });
        }
    }

    public void SelectSubfolder(string relativePath)
    {
        var raw = relativePath ?? string.Empty;
        SelectedSubfolderRelative = (string.Equals(raw, "Root", StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(raw))
            ? string.Empty
            : raw;

        // Reset visual tile highlights
        var accentBrush = Application.Current.Resources["AccentFillColorDefaultBrush"] as Brush;
        var defaultBorderBrush = Application.Current.Resources["CardStrokeColorDefaultBrush"] as Brush;

        bool isUnorganised = string.IsNullOrEmpty(SelectedSubfolderRelative);
        bool isShared = string.Equals(SelectedSubfolderRelative, "Shared Docs", StringComparison.OrdinalIgnoreCase);

        BtnTileUnorganised.BorderBrush = isUnorganised ? accentBrush : defaultBorderBrush;
        BtnTileUnorganised.BorderThickness = isUnorganised ? new Thickness(2) : new Thickness(1);

        BtnTileShared.BorderBrush = isShared ? accentBrush : defaultBorderBrush;
        BtnTileShared.BorderThickness = isShared ? new Thickness(2) : new Thickness(1);

        foreach (var sub in AppSubfolders)
        {
            sub.IsSelected = string.Equals(SelectedSubfolderRelative, sub.RelativePath, StringComparison.OrdinalIgnoreCase);
        }

        // Update Header Toolbar
        if (isUnorganised)
        {
            TxtCurrentFolderName.Text = AppServices.Localization.GetString("Sessions.TileLooseFilesTitle", "Unorganised Files");
            CurrentFolderGlyph.Glyph = "\uE8B7";
            CurrentFolderGlyph.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 245, 158, 11)); // Amber
        }
        else if (isShared)
        {
            TxtCurrentFolderName.Text = AppServices.Localization.GetString("Sessions.TileSharedDocsTitle", "Shared Docs");
            CurrentFolderGlyph.Glyph = "\uE8A5";
            CurrentFolderGlyph.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 2, 132, 199)); // Blue
        }
        else
        {
            TxtCurrentFolderName.Text = SelectedSubfolderRelative;
            CurrentFolderGlyph.Glyph = "\uED25";
            CurrentFolderGlyph.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 147, 51, 234)); // Purple
        }

        LoadFilesForCurrentSubfolder();
    }

    public void LoadFilesForCurrentSubfolder()
    {
        Files.Clear();
        var targetDir = CurrentSubfolderFullPath;

        if (!Directory.Exists(targetDir))
        {
            UpdateEmptyState();
            return;
        }

        var loaded = AppServices.FolderManager.GetFolderFiles(targetDir).ToList();
        if (string.IsNullOrEmpty(SelectedSubfolderRelative))
        {
            var legacySub = Path.Combine(CustomerFolderPath, "00_Unorganised");
            if (Directory.Exists(legacySub))
            {
                foreach (var f in AppServices.FolderManager.GetFolderFiles(legacySub))
                {
                    if (!loaded.Any(x => string.Equals(x.FullPath, f.FullPath, StringComparison.OrdinalIgnoreCase)))
                    {
                        loaded.Add(f);
                    }
                }
            }
        }
        foreach (var item in loaded)
        {
            Files.Add(item);
        }

        long totalBytes = loaded.Sum(f => f.FileSizeBytes);
        string formattedTotal = totalBytes < 1024 * 1024
            ? $"{totalBytes / 1024.0:F1} KB"
            : $"{totalBytes / (1024.0 * 1024.0):F1} MB";

        var fileCountWord = loaded.Count == 1
            ? AppServices.Localization.GetString("FolderBrowser.FileCountSingle", "file")
            : AppServices.Localization.GetString("FolderBrowser.FilesCount", "files");

        TxtFolderStatsSummary.Text = $"{loaded.Count} {fileCountWord} • {formattedTotal}";

        UpdateEmptyState();
    }

    private void UpdateEmptyState()
    {
        EmptyStateBorder.Visibility = Files.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        FilesListView.Visibility = Files.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void SubfolderTile_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn)
        {
            var raw = btn.Tag?.ToString() ?? string.Empty;
            SelectSubfolder(raw);
        }
    }

    private void AppSubfolderChip_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn)
        {
            var raw = btn.Tag?.ToString() ?? string.Empty;
            SelectSubfolder(raw);
        }
    }

    private void OpenFolderInExplorer_Click(object sender, RoutedEventArgs e)
    {
        AppServices.FolderManager.OpenFolderInExplorer(CurrentSubfolderFullPath);
    }

    private void RefreshFolder_Click(object sender, RoutedEventArgs e)
    {
        RefreshSubfolderStrip();
        LoadFilesForCurrentSubfolder();
    }

    private async void AddFile_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var targetDir = CurrentSubfolderFullPath;
            if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);

            var file = await AppServices.Pickers.PickFileAsync(["*"]);
            if (file != null)
            {
                var destPath = Path.Combine(targetDir, Path.GetFileName(file));
                File.Copy(file, destPath, overwrite: true);
                RefreshFolder_Click(this, new RoutedEventArgs());
            }
        }
        catch { }
    }

    private async void AddSubfolder_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(CustomerFolderPath)) return;

        var (result, rawName) = await AppServices.Dialogs.ShowInputAsync(
            "Create Application Subfolder",
            "",
            "e.g. SSC CGL, PAN Card, Scholarship",
            "Create",
            "Cancel"
        );

        if (result == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(rawName))
        {
            AppServices.FolderManager.EnsureApplicationSubfolder(CustomerFolderPath, rawName.Trim());
            RefreshSubfolderStrip();
            SelectSubfolder(rawName.Trim());
        }
    }

    private void FileRow_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        FolderFileItem? item = null;
        if (sender is FrameworkElement fe)
        {
            item = fe.Tag as FolderFileItem ?? fe.DataContext as FolderFileItem;
        }

        if (item == null || item.IsRenaming || string.IsNullOrEmpty(item.FullPath)) return;

        AppServices.FolderManager.OpenFileWithDefaultApp(item.FullPath);
    }

    private void FilesListView_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            if (FilesListView.SelectedItem is FolderFileItem item && !item.IsRenaming && !string.IsNullOrEmpty(item.FullPath))
            {
                AppServices.FolderManager.OpenFileWithDefaultApp(item.FullPath);
                e.Handled = true;
            }
        }
    }


    private void StartRename_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is FolderFileItem item)
        {
            // Close any other open inline renames
            foreach (var f in Files)
            {
                if (f != item && f.IsRenaming) f.IsRenaming = false;
            }

            item.EditName = item.Name;
            item.IsRenaming = true;
        }
    }

    private void RenameTextBox_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox tb)
        {
            tb.Focus(FocusState.Programmatic);
            var text = tb.Text ?? string.Empty;
            var ext = Path.GetExtension(text);
            int baseLength = string.IsNullOrEmpty(ext) ? text.Length : Math.Max(0, text.Length - ext.Length);
            tb.Select(0, baseLength);
        }
    }

    private void RenameTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox tb && tb.Tag is FolderFileItem item && item.IsRenaming)
        {
            CommitRename(item, tb.Text);
        }
    }

    private void SaveRename_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is FolderFileItem item)
        {
            CommitRename(item);
        }
    }

    private void CancelRename_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is FolderFileItem item)
        {
            item.EditName = item.Name;
            item.IsRenaming = false;
        }
    }

    private void RenameTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (sender is TextBox tb && tb.Tag is FolderFileItem item)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                e.Handled = true;
                CommitRename(item, tb.Text);
            }
            else if (e.Key == Windows.System.VirtualKey.Escape)
            {
                e.Handled = true;
                item.EditName = item.Name;
                item.IsRenaming = false;
            }
        }
    }

    private async void CommitRename(FolderFileItem item, string? customText = null)
    {
        var textToUse = (customText ?? item.EditName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(textToUse))
        {
            item.IsRenaming = false;
            return;
        }

        // Auto-preserve extension: if new name lacks the original extension, append it
        var oldExt = Path.GetExtension(item.FullPath);
        if (!string.IsNullOrEmpty(oldExt) && !textToUse.EndsWith(oldExt, StringComparison.OrdinalIgnoreCase))
        {
            textToUse += oldExt;
        }

        if (string.Equals(textToUse, item.Name, StringComparison.OrdinalIgnoreCase))
        {
            item.IsRenaming = false;
            return;
        }

        if (AppServices.FolderManager.RenameFile(item.FullPath, textToUse, out string newFullPath, out string error))
        {
            item.FullPath = newFullPath;
            item.Name = Path.GetFileName(newFullPath);
            item.EditName = item.Name;
            item.Extension = Path.GetExtension(newFullPath).ToLowerInvariant();
            item.IsRenaming = false;
        }
        else
        {
            item.IsRenaming = false;
            await AppServices.Dialogs.ShowAlertAsync("Rename Failed", error);
        }
    }

    private void OpenInDefaultApp_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is FolderFileItem item)
        {
            AppServices.FolderManager.OpenFileWithDefaultApp(item.FullPath);
        }
    }

    private async void DeleteFile_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is FolderFileItem item)
        {
            var title = AppServices.Localization.GetString("FolderBrowser.DeleteConfirmTitle", "Delete File");
            var msg = string.Format(
                AppServices.Localization.GetString("FolderBrowser.DeleteConfirmMessage", "Are you sure you want to permanently delete '{0}'?"),
                item.Name);

            var res = await AppServices.Dialogs.ShowConfirmationAsync(title, msg, "Delete", "Cancel");
            if (res == ContentDialogResult.Primary)
            {
                if (AppServices.FolderManager.DeleteFile(item.FullPath, out string err))
                {
                    Files.Remove(item);
                    RefreshSubfolderStrip();
                    TxtFolderStatsSummary.Text = $"{Files.Count} files";
                    UpdateEmptyState();
                }
                else
                {
                    await AppServices.Dialogs.ShowAlertAsync("Delete Failed", err);
                }
            }
        }
    }

    private void SmartTagButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is FolderFileItem item)
        {
            var flyout = SmartTagHelper.CreateTagFlyout(item, async (tag) =>
            {
                await ApplySmartTagAsync(item, tag);
            });
            flyout.ShowAt(btn);
        }
    }

    private async Task ApplySmartTagAsync(FolderFileItem item, string tag)
    {
        string? targetTag = tag;
        if (tag == "custom")
        {
            targetTag = await SmartTagHelper.PromptCustomTagAsync(XamlRoot);
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

            // If tagged as photo and is an image, set as customer profile photo
            if (tag == "photo" && item.IsImage)
            {
                await SetCustomerPhotoAsync(newFullPath);
            }
        }
        else
        {
            await AppServices.Dialogs.ShowAlertAsync("Tagging Failed", error);
        }
    }

    private async Task SetCustomerPhotoAsync(string photoPath)
    {
        var customer = CurrentCustomer;
        if (customer == null && !string.IsNullOrWhiteSpace(CustomerFolderPath))
        {
            var folderName = Path.GetFileName(CustomerFolderPath);
            var match = System.Text.RegularExpressions.Regex.Match(folderName, @"\((CUST-\d+)\)");
            if (match.Success)
            {
                var code = match.Groups[1].Value;
                var found = await AppServices.Customers.SearchAsync(code);
                customer = found.FirstOrDefault();
            }
        }

        if (customer != null)
        {
            customer.PhotoPath = photoPath;
            await AppServices.Customers.UpdatePhotoAsync(customer.Id, photoPath);
            CustomerPhotoUpdated?.Invoke(photoPath);
        }
    }
}
