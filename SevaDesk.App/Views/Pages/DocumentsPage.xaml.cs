using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Text;
using SevaDesk_App.ViewModels.Pages;
using SevaDesk.Core.Models;
using SevaDesk_App.Services;

namespace SevaDesk_App.Views.Pages;

public sealed partial class DocumentsPage : Page
{
    public DocumentsViewModel ViewModel { get; } = new();
    private bool _suppressInspectorChipClick = false;
    private System.IO.FileSystemWatcher? _watcher;
    private DispatcherTimer? _watcherDebounceTimer;

    public DocumentsPage()
    {
        InitializeComponent();
        ViewModel.SelectedDocumentTagsChanged += PopulateInspectorChips;
        Loaded += (s, e) =>
        {
            PopulateInspectorChips();
            SetupWatcher();
        };
        Unloaded += (s, e) =>
        {
            CleanupWatcher();
        };
    }

    private void SetupWatcher()
    {
        CleanupWatcher();
        var baseDir = AppServices.FolderManager?.BaseDirectory;
        if (string.IsNullOrWhiteSpace(baseDir) || !System.IO.Directory.Exists(baseDir)) return;

        try
        {
            _watcherDebounceTimer = new DispatcherTimer { Interval = System.TimeSpan.FromMilliseconds(400) };
            _watcherDebounceTimer.Tick += async (s, e) =>
            {
                _watcherDebounceTimer.Stop();

                // If user is currently editing a filename in inspector or list, wait
                if (ViewModel.PendingDocuments.Any(d => d.IsRenaming) || ViewModel.ProcessedDocuments.Any(d => d.IsRenaming))
                    return;

                var currentSelectedPath = ViewModel.SelectedDocument?.FilePath;
                await ViewModel.LoadDocumentsAsync();

                if (!string.IsNullOrEmpty(currentSelectedPath))
                {
                    var restored = ViewModel.PendingDocuments.FirstOrDefault(d => string.Equals(d.FilePath, currentSelectedPath, System.StringComparison.OrdinalIgnoreCase))
                                ?? ViewModel.ProcessedDocuments.FirstOrDefault(d => string.Equals(d.FilePath, currentSelectedPath, System.StringComparison.OrdinalIgnoreCase));
                    if (restored != null)
                    {
                        ViewModel.SelectedDocument = restored;
                    }
                }
            };

            _watcher = new System.IO.FileSystemWatcher(baseDir)
            {
                IncludeSubdirectories = true,
                NotifyFilter = System.IO.NotifyFilters.FileName | System.IO.NotifyFilters.DirectoryName | System.IO.NotifyFilters.LastWrite | System.IO.NotifyFilters.Size,
                EnableRaisingEvents = true
            };

            _watcher.Created += OnFolderChanged;
            _watcher.Deleted += OnFolderChanged;
            _watcher.Renamed += OnFolderChanged;
            _watcher.Changed += OnFolderChanged;
        }
        catch { }
    }

    private void OnFolderChanged(object sender, System.IO.FileSystemEventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            _watcherDebounceTimer?.Stop();
            _watcherDebounceTimer?.Start();
        });
    }

    private void CleanupWatcher()
    {
        try
        {
            _watcher?.Dispose();
            _watcher = null;
            _watcherDebounceTimer?.Stop();
            _watcherDebounceTimer = null;
        }
        catch { }
    }

    public static bool HasStatus(string status) => !string.IsNullOrWhiteSpace(status);
    public static Visibility VisibleIf(bool condition) => condition ? Visibility.Visible : Visibility.Collapsed;
    public static Visibility CollapsedIf(bool condition) => condition ? Visibility.Collapsed : Visibility.Visible;
    public static bool Not(bool condition) => !condition;

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.LoadDocumentsAsync();
    }

    private void SyncToBackup_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.SyncToBackupCommand.Execute(null);
    }

    private void OpenExplorer_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.OpenInExplorerCommand.Execute(null);
    }

    private void PresetButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is CompressionPreset preset)
        {
            ViewModel.ApplyPresetCommand.Execute(preset);
        }
    }

    private void OpenFile_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.OpenFile();
    }

    private void PendingFileRow_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        DocumentItem? doc = null;
        if (sender is FrameworkElement fe)
        {
            doc = fe.Tag as DocumentItem ?? fe.DataContext as DocumentItem;
        }

        if (doc == null || doc.IsRenaming || string.IsNullOrWhiteSpace(doc.FilePath) || !System.IO.File.Exists(doc.FilePath)) return;

        AppServices.FolderManager.OpenFileWithDefaultApp(doc.FilePath);
    }

    private void ProcessedFileRow_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        DocumentItem? doc = null;
        if (sender is FrameworkElement fe)
        {
            doc = fe.Tag as DocumentItem ?? fe.DataContext as DocumentItem;
        }

        if (doc == null || string.IsNullOrWhiteSpace(doc.FilePath) || !System.IO.File.Exists(doc.FilePath)) return;

        AppServices.FolderManager.OpenFileWithDefaultApp(doc.FilePath);
    }

    private void PendingListView_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            if (ViewModel.SelectedDocument != null && !ViewModel.SelectedDocument.IsRenaming &&
                !string.IsNullOrWhiteSpace(ViewModel.SelectedDocument.FilePath) &&
                System.IO.File.Exists(ViewModel.SelectedDocument.FilePath))
            {
                AppServices.FolderManager.OpenFileWithDefaultApp(ViewModel.SelectedDocument.FilePath);
                e.Handled = true;
            }
        }
    }

    private void ProcessedListView_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            if (sender is ListView lv && lv.SelectedItem is DocumentItem doc &&
                !string.IsNullOrWhiteSpace(doc.FilePath) &&
                System.IO.File.Exists(doc.FilePath))
            {
                AppServices.FolderManager.OpenFileWithDefaultApp(doc.FilePath);
                e.Handled = true;
            }
        }
    }


    private async void MoveToSubfolder_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string subfolderName && !string.IsNullOrWhiteSpace(subfolderName))
        {
            await ViewModel.MoveDocumentToSubfolderAsync(subfolderName);
        }
    }

    private async void NewSubfolder_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedDocument == null) return;

        var customerRoot = ViewModel.GetCustomerRootFolder(ViewModel.SelectedDocument);
        if (string.IsNullOrWhiteSpace(customerRoot))
        {
            ViewModel.ShowError("Cannot determine customer working directory.");
            return;
        }

        var (result, rawName) = await AppServices.Dialogs.ShowInputAsync(
            "Create Customer Subfolder",
            $"Target: {ViewModel.SelectedDocument.CustomerName}",
            "e.g. SSC CGL, PAN Card, Scholarship",
            "Create & Move File",
            "Create Only"
        );
        rawName = rawName?.Trim();
        if (string.IsNullOrWhiteSpace(rawName)) return;

        try
        {
            var appPath = AppServices.FolderManager.EnsureApplicationSubfolder(customerRoot, rawName);
            var subfolderName = System.IO.Path.GetFileName(appPath);

            if (result == ContentDialogResult.Primary)
            {
                await ViewModel.MoveDocumentToSubfolderAsync(subfolderName);
            }
            else if (result == ContentDialogResult.Secondary)
            {
                ViewModel.RefreshCustomerSubfolders();
                ViewModel.ShowSuccess($"Subfolder '{subfolderName}' created.");
            }
        }
        catch (Exception ex)
        {
            ViewModel.ShowError($"Error creating subfolder: {ex.Message}");
        }
    }

    private async void MoveToShared_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.MoveDocumentToSubfolderAsync("Shared Docs");
    }

    private async void MoveToApplications_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.MoveDocumentToSubfolderAsync("Applications");
    }

    private async void MoveToPrint_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.MoveDocumentToSubfolderAsync("Ready to Print");
    }

    private void DocNav_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is NavigationViewItem item)
        {
            if (item.Tag?.ToString() == "Studio")
            {
                OrgView.Visibility = Visibility.Collapsed;
                StudioPlaceholderView.Visibility = Visibility.Visible;
            }
            else
            {
                OrgView.Visibility = Visibility.Visible;
                StudioPlaceholderView.Visibility = Visibility.Collapsed;
            }
        }
    }

    private void BackToOrganizer_Click(object sender, RoutedEventArgs e)
    {
        DocNav.SelectedItem = DocNav.MenuItems[0];
        OrgView.Visibility = Visibility.Visible;
        StudioPlaceholderView.Visibility = Visibility.Collapsed;
    }

    private void PopulateInspectorChips()
    {
        InspectorChipsPanel.Children.Clear();
        if (ViewModel.SelectedDocument == null) return;

        // 1. Unused tags first (standard & custom)
        foreach (var tag in ViewModel.CurrentDocumentTags.Where(t => !t.IsUsed))
        {
            var btn = CreateInspectorChipButton(tag);
            InspectorChipsPanel.Children.Add(btn);
        }

        // 2. [+ Custom...] chip
        var customBtn = CreateCustomInspectorChipButton();
        InspectorChipsPanel.Children.Add(customBtn);

        // 3. Already-existing tags at the very end
        foreach (var tag in ViewModel.CurrentDocumentTags.Where(t => t.IsUsed))
        {
            var btn = CreateInspectorChipButton(tag);
            InspectorChipsPanel.Children.Add(btn);
        }
    }

    private Button CreateInspectorChipButton(DocumentTagItem tag)
    {
        var chip = new Button
        {
            Height = 30,
            CornerRadius = new CornerRadius(15),
            Padding = new Thickness(10, 3, tag.IsCustom ? 5 : 10, 3),
            Tag = tag.TagKey
        };

        var sp = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
        var icon = new FontIcon { FontSize = 11 };
        var text = new TextBlock { FontSize = 12 };

        if (tag.IsUsed)
        {
            icon.Glyph = "\uE73E"; // Checkmark
            icon.Foreground = (Brush)Application.Current.Resources["SystemFillColorSuccessBrush"];
            text.Text = $"✓ {tag.DisplayName}";
            chip.Opacity = 0.6;
            ToolTipService.SetToolTip(chip, $"Already exists in customer folder. Click to rename as an additional copy ({tag.TagKey}_2).");
        }
        else
        {
            icon.Glyph = tag.Glyph;
            text.Text = tag.DisplayName;
            ToolTipService.SetToolTip(chip, $"Click to rename as '{tag.TagKey}'");
        }

        sp.Children.Add(icon);
        sp.Children.Add(text);

        if (tag.IsCustom)
        {
            var delBtn = new Button
            {
                Width = 20,
                Height = 20,
                Padding = new Thickness(0),
                CornerRadius = new CornerRadius(10),
                Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0)),
                BorderThickness = new Thickness(0),
                Margin = new Thickness(4, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Content = new FontIcon { Glyph = "\uE711", FontSize = 9, Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"] }
            };
            ToolTipService.SetToolTip(delBtn, $"Delete saved tag '{tag.DisplayName}'");
            delBtn.Click += async (s, e) =>
            {
                _suppressInspectorChipClick = true;
                try
                {
                    await AppServices.CustomTags.DeleteAsync(tag.TagKey);
                    PopulateInspectorChips();
                    ViewModel.ShowInfo($"Removed custom tag '{tag.DisplayName}'");
                }
                finally
                {
                    await Task.Delay(150);
                    _suppressInspectorChipClick = false;
                }
            };
            sp.Children.Add(delBtn);
        }

        chip.Content = sp;

        chip.Click += async (s, e) =>
        {
            if (_suppressInspectorChipClick) return;

            if (ViewModel.SelectedDocument == null) return;
            var (success, _) = await ViewModel.RenameDocumentAsync(ViewModel.SelectedDocument, tag.TagKey);
            if (success)
            {
                PopulateInspectorChips();
            }
        };

        return chip;
    }

    private Button CreateCustomInspectorChipButton()
    {
        var customBtn = new Button
        {
            Height = 30,
            CornerRadius = new CornerRadius(15),
            Padding = new Thickness(10, 3, 10, 3)
        };

        var sp = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
        sp.Children.Add(new FontIcon { Glyph = "\uE710", FontSize = 10 });
        sp.Children.Add(new TextBlock { Text = "Custom...", FontSize = 12 });
        customBtn.Content = sp;
        ToolTipService.SetToolTip(customBtn, "Enter custom file name (saved automatically)");

        var flyout = new Flyout();
        var flyoutStack = new StackPanel { Width = 220, Spacing = 8 };
        flyoutStack.Children.Add(new TextBlock { Text = "Custom File Name", FontWeight = FontWeights.SemiBold, FontSize = 12 });

        var tb = new TextBox { PlaceholderText = "e.g. voter_id, ration_card", FontSize = 12 };
        flyoutStack.Children.Add(tb);

        var applyBtn = new Button
        {
            Content = "Rename File",
            Style = (Style)Application.Current.Resources["AccentButtonStyle"],
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        applyBtn.Click += async (s, e) =>
        {
            var raw = tb.Text?.Trim();
            if (string.IsNullOrWhiteSpace(raw) || ViewModel.SelectedDocument == null) return;
            var saved = await SmartTagHelper.SaveCustomTagAsync(raw);
            var cleanTag = saved?.TagKey ?? SmartTagHelper.NormalizeTagKey(raw);
            flyout.Hide();
            var (success, _) = await ViewModel.RenameDocumentAsync(ViewModel.SelectedDocument, cleanTag);
            if (success)
            {
                PopulateInspectorChips();
            }
        };

        flyoutStack.Children.Add(applyBtn);
        flyout.Content = flyoutStack;
        customBtn.Flyout = flyout;

        return customBtn;
    }

    private async void RowTagButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is DocumentItem doc)
        {
            var flyout = new MenuFlyout();

            var tags = await ViewModel.GetTagsForDocumentAsync(doc);
            var standardTagsInMenu = tags.Where(t => !t.IsCustom).ToList();
            var customTagsInMenu = tags.Where(t => t.IsCustom).ToList();

            foreach (var tag in standardTagsInMenu)
            {
                var item = new MenuFlyoutItem
                {
                    Text = tag.IsUsed ? $"✓ {tag.DisplayName}" : tag.DisplayName,
                    Icon = new FontIcon { Glyph = tag.Glyph }
                };
                item.Click += async (s, args) =>
                {
                    await ViewModel.RenameDocumentAsync(doc, tag.TagKey);
                    if (doc == ViewModel.SelectedDocument)
                    {
                        PopulateInspectorChips();
                    }
                };
                flyout.Items.Add(item);
            }

            if (customTagsInMenu.Count > 0)
            {
                flyout.Items.Add(new MenuFlyoutSeparator());
                foreach (var tag in customTagsInMenu)
                {
                    var item = new MenuFlyoutItem
                    {
                        Text = tag.IsUsed ? $"✓ {tag.DisplayName}" : tag.DisplayName,
                        Icon = new FontIcon { Glyph = tag.Glyph, Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 139, 92, 246)) }
                    };
                    item.Click += async (s, args) =>
                    {
                        await ViewModel.RenameDocumentAsync(doc, tag.TagKey);
                        if (doc == ViewModel.SelectedDocument)
                        {
                            PopulateInspectorChips();
                        }
                    };
                    flyout.Items.Add(item);
                }

                var delSubMenu = new MenuFlyoutSubItem
                {
                    Text = "Delete Saved Tag...",
                    Icon = new FontIcon { Glyph = "\uE74D", Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 239, 68, 68)) }
                };
                foreach (var tag in customTagsInMenu)
                {
                    var delItem = new MenuFlyoutItem
                    {
                        Text = $"Delete '{tag.DisplayName}'",
                        Icon = new FontIcon { Glyph = "\uE711" }
                    };
                    delItem.Click += async (s, args) =>
                    {
                        await AppServices.CustomTags.DeleteAsync(tag.TagKey);
                        if (doc == ViewModel.SelectedDocument)
                        {
                            PopulateInspectorChips();
                        }
                        ViewModel.ShowInfo($"Removed custom tag '{tag.DisplayName}'");
                    };
                    delSubMenu.Items.Add(delItem);
                }
                flyout.Items.Add(delSubMenu);
            }

            flyout.Items.Add(new MenuFlyoutSeparator());

            var customItem = new MenuFlyoutItem
            {
                Text = "Custom Tag...",
                Icon = new FontIcon { Glyph = "\uE70F" }
            };
            customItem.Click += async (s, args) =>
            {
                var cleanTag = await SmartTagHelper.PromptCustomTagAsync(XamlRoot);
                if (!string.IsNullOrWhiteSpace(cleanTag))
                {
                    await ViewModel.RenameDocumentAsync(doc, cleanTag);
                    if (doc == ViewModel.SelectedDocument)
                    {
                        PopulateInspectorChips();
                    }
                }
            };
            flyout.Items.Add(customItem);

            flyout.ShowAt(btn);
        }
    }

    private void StartInspectorRename_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedDocument == null) return;
        var nameWithoutExt = System.IO.Path.GetFileNameWithoutExtension(ViewModel.SelectedDocument.Name);
        ViewModel.SelectedDocument.EditName = nameWithoutExt;
        ViewModel.SelectedDocument.IsRenaming = true;
    }

    private async void SaveInspectorRename_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedDocument == null) return;
        var raw = ViewModel.SelectedDocument.EditName?.Trim();
        if (string.IsNullOrWhiteSpace(raw))
        {
            ViewModel.SelectedDocument.IsRenaming = false;
            return;
        }

        var (success, _) = await ViewModel.RenameDocumentAsync(ViewModel.SelectedDocument, raw);
        if (success)
        {
            PopulateInspectorChips();
        }
    }

    private void CancelInspectorRename_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedDocument != null)
        {
            ViewModel.SelectedDocument.IsRenaming = false;
        }
    }

    private void InspectorRename_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            SaveInspectorRename_Click(sender, e);
            e.Handled = true;
        }
        else if (e.Key == Windows.System.VirtualKey.Escape)
        {
            CancelInspectorRename_Click(sender, e);
            e.Handled = true;
        }
    }
}
