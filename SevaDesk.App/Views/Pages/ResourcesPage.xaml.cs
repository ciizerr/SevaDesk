using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using SevaDesk.Core.Models;
using SevaDesk_App.Services;
using SevaDesk_App.ViewModels.Pages;
using Windows.UI;

namespace SevaDesk_App.Views.Pages;

public sealed partial class ResourcesPage : Page
{
    public ResourcesViewModel ViewModel { get; } = new();

    public ResourcesPage()
    {
        InitializeComponent();

        ViewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(ResourcesViewModel.SelectedResource))
            {
                DispatcherQueue.TryEnqueue(UpdateRequiredDocsChips);
            }
        };
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        DispatcherQueue.TryEnqueue(UpdateRequiredDocsChips);
    }

    // --- UI Helper Converters ---
    public static Visibility VisibleIf(bool condition) => condition ? Visibility.Visible : Visibility.Collapsed;
    public static Visibility CollapsedIf(bool condition) => condition ? Visibility.Collapsed : Visibility.Visible;
    public static Visibility VisibleIfHasResource(ResourceItem? item) => item != null ? Visibility.Visible : Visibility.Collapsed;

    public Style? FilterButtonStyle(string activeCategory, string currentCategory)
    {
        if (string.Equals(activeCategory, currentCategory, StringComparison.OrdinalIgnoreCase))
        {
            return (Style)Application.Current.Resources["AccentButtonStyle"];
        }
        return null;
    }

    public static Brush FileStatusBadgeBackground(bool hasFile) =>
        hasFile
            ? new SolidColorBrush(Color.FromArgb(35, 16, 185, 129)) // Emerald green tint
            : new SolidColorBrush(Color.FromArgb(35, 245, 158, 11)); // Amber tint

    public static Brush FileStatusBadgeForeground(bool hasFile) =>
        hasFile
            ? new SolidColorBrush(Color.FromArgb(255, 16, 185, 129))
            : new SolidColorBrush(Color.FromArgb(255, 245, 158, 11));

    public static string FileStatusBadgeGlyph(bool hasFile) => hasFile ? "\uE73E" : "\uE7BA";

    // --- Filter Buttons Handlers ---
    private void CategoryAll_Click(object sender, RoutedEventArgs e) => ViewModel.SetCategoryCommand.Execute("All");
    private void CategoryAffidavits_Click(object sender, RoutedEventArgs e) => ViewModel.SetCategoryCommand.Execute("Affidavits");
    private void CategoryForms_Click(object sender, RoutedEventArgs e) => ViewModel.SetCategoryCommand.Execute("Blank Forms");
    private void CategoryGuidelines_Click(object sender, RoutedEventArgs e) => ViewModel.SetCategoryCommand.Execute("Guidelines");

    // --- Selection and Text Change Handlers ---
    private void ListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateRequiredDocsChips();
    }

    private void RequiredDocs_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateRequiredDocsChips();
    }

    private void UpdateRequiredDocsChips()
    {
        if (RequiredDocsChipsPanel == null) return;
        RequiredDocsChipsPanel.Children.Clear();

        var docs = ViewModel.SelectedResource?.RequiredDocsList?.ToList();
        if (docs == null || docs.Count == 0)
        {
            RequiredDocsChipsPanel.Children.Add(new TextBlock
            {
                Text = "No supporting documents specified",
                FontSize = 11,
                Foreground = (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"],
                FontStyle = Windows.UI.Text.FontStyle.Italic,
                VerticalAlignment = VerticalAlignment.Center
            });
            return;
        }

        foreach (var doc in docs)
        {
            var border = new Border
            {
                Background = (Brush)Application.Current.Resources["LayerFillColorAltBrush"],
                BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(10, 4, 10, 4)
            };

            var sp = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 5
            };

            sp.Children.Add(new FontIcon
            {
                Glyph = "\uE73E",
                FontSize = 10,
                Foreground = (Brush)Application.Current.Resources["AccentTextFillColorPrimaryBrush"],
                VerticalAlignment = VerticalAlignment.Center
            });

            sp.Children.Add(new TextBlock
            {
                Text = doc,
                FontSize = 11,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center
            });

            border.Child = sp;
            RequiredDocsChipsPanel.Children.Add(border);
        }
    }

    // --- Actions Handlers ---
    private async void ToggleFavorite_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is ResourceItem item)
        {
            await ViewModel.ToggleFavoriteAsync(item);
        }
    }

    private void ListItemOpen_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement elem && elem.Tag is ResourceItem item)
        {
            ViewModel.OpenDocument(item);
        }
    }

    private void QuickPrint_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement elem && elem.Tag is ResourceItem item)
        {
            ViewModel.QuickPrint(item);
        }
    }

    private async void ListItemCopyToFolder_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement elem && elem.Tag is ResourceItem item)
        {
            await ViewModel.CopyToActiveSessionAsync(item);
        }
    }

    private async void ListItemDelete_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement elem && elem.Tag is ResourceItem item)
        {
            await ShowDeleteConfirmationAsync(item);
        }
    }

    private void InspectorOpen_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.OpenDocument(ViewModel.SelectedResource);
    }

    private void InspectorOpenFileLocation_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.OpenFileLocation(ViewModel.SelectedResource);
    }

    private async void InspectorAttachFile_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedResource == null) return;

        try
        {
            var path = await AppServices.Pickers.PickFileAsync(new[] { ".pdf", ".docx", ".doc", ".xlsx", ".xls", ".txt", ".jpg", ".png" });
            if (!string.IsNullOrWhiteSpace(path))
            {
                await ViewModel.AttachFileToResourceAsync(ViewModel.SelectedResource, path);
            }
        }
        catch (Exception ex)
        {
            ViewModel.ShowError($"File picker error: {ex.Message}");
        }
    }

    private void InspectorPrint_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.QuickPrint(ViewModel.SelectedResource);
    }

    private async void InspectorCopyToFolder_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedResource != null)
        {
            await ViewModel.CopyToActiveSessionAsync(ViewModel.SelectedResource);
        }
    }

    private async void InspectorDelete_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedResource != null)
        {
            await ShowDeleteConfirmationAsync(ViewModel.SelectedResource);
        }
    }

    private async void SaveDetails_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.SaveSelectedResourceAsync();
        UpdateRequiredDocsChips();
    }

    private async Task ShowDeleteConfirmationAsync(ResourceItem item)
    {
        if (item == null) return;

        var panel = new StackPanel { Spacing = 12, Width = 460 };

        // Caution / Danger Alert Card
        var alertBorder = new Border
        {
            Background = (Brush)Application.Current.Resources["SubtleFillColorSecondaryBrush"],
            BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(14, 12, 14, 12)
        };

        var grid = new Grid { ColumnSpacing = 12 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var icon = new FontIcon
        {
            Glyph = "\uE7BA",
            FontSize = 20,
            Foreground = (Brush)Application.Current.Resources["SystemFillColorCriticalBrush"],
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 2, 0, 0)
        };
        Grid.SetColumn(icon, 0);
        grid.Children.Add(icon);

        var headerStack = new StackPanel
        {
            Spacing = 4,
            Children =
            {
                new TextBlock
                {
                    Text = $"Delete \"{item.Title}\"?",
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    FontSize = 13,
                    TextWrapping = TextWrapping.Wrap
                },
                new TextBlock
                {
                    Text = $"{item.Category} • {item.FileType}",
                    FontSize = 11,
                    Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
                }
            }
        };
        Grid.SetColumn(headerStack, 1);
        grid.Children.Add(headerStack);
        alertBorder.Child = grid;
        panel.Children.Add(alertBorder);

        // Attached physical document file card
        if (item.HasFile && !string.IsNullOrWhiteSpace(item.FilePath))
        {
            var fileCard = new Border
            {
                Background = (Brush)Application.Current.Resources["SubtleFillColorSecondaryBrush"],
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12, 10, 12, 10),
                Child = new StackPanel
                {
                    Spacing = 4,
                    Children =
                    {
                        new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            Spacing = 6,
                            Children =
                            {
                                new FontIcon { Glyph = "\uE8B7", FontSize = 12, Foreground = (Brush)Application.Current.Resources["SystemFillColorCriticalBrush"] },
                                new TextBlock { Text = "Physical File Will Be Deleted", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, FontSize = 12 }
                            }
                        },
                        new TextBlock
                        {
                            Text = item.FilePath,
                            FontSize = 11,
                            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                            TextWrapping = TextWrapping.Wrap,
                            MaxLines = 2,
                            TextTrimming = TextTrimming.CharacterEllipsis
                        }
                    }
                }
            };
            panel.Children.Add(fileCard);
        }

        panel.Children.Add(new TextBlock
        {
            Text = "This will permanently delete the template and its file from disk.",
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12,
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
        });

        var dialog = new ContentDialog
        {
            XamlRoot = this.XamlRoot ?? MainWindow.Instance?.Content?.XamlRoot,
            Title = "Delete Template",
            Content = panel,
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close // Focused on Cancel for safety
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            await ViewModel.DeleteResourceAsync(item);
            UpdateRequiredDocsChips();
        }
    }

    private static TextBlock CreateRequiredHeader(string label)
    {
        var tb = new TextBlock { FontSize = 12, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold };
        tb.Inlines.Add(new Run { Text = label + " " });
        tb.Inlines.Add(new Run { Text = "*", Foreground = new SolidColorBrush(Color.FromArgb(255, 239, 68, 68)), FontWeight = Microsoft.UI.Text.FontWeights.Bold });
        return tb;
    }

    // --- Add New Resource Dialog with File Picker & Hierarchy ---
    private async void AddNewResource_Click(object sender, RoutedEventArgs e)
    {
        string? selectedSourcePath = null;

        var btnPickFile = new Button
        {
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 6,
                Children =
                {
                    new FontIcon { Glyph = "\uED25", FontSize = 13 },
                    new TextBlock { Text = "Choose Document File (PDF / DOCX / Image)...", FontSize = 12 }
                }
            },
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(0, 4, 0, 4)
        };

        var lblChosenFile = new TextBlock
        {
            Text = "No file chosen yet (you can also attach one later)",
            FontSize = 11,
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = new Thickness(0, 0, 0, 8)
        };

        var txtTitle = new TextBox
        {
            PlaceholderText = "e.g. Income Declaration Affidavit 2026",
            Margin = new Thickness(0, 4, 0, 8)
        };

        var cmbCategory = new ComboBox
        {
            ItemsSource = new[] { "Blank Forms", "Affidavits", "Guidelines", "Rate Cards", "Identity & Tax", "Certificates" },
            SelectedIndex = 0,
            IsEditable = true,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(0, 4, 0, 8)
        };

        var txtRequiredDocs = new TextBox
        {
            PlaceholderText = "e.g. Aadhaar Card, Passport Photo, Marksheet",
            Margin = new Thickness(0, 4, 0, 8)
        };

        var txtNotes = new TextBox
        {
            PlaceholderText = "e.g. Must be on ₹10 stamp paper, notarization required...",
            TextWrapping = TextWrapping.Wrap,
            AcceptsReturn = true,
            Height = 56,
            Margin = new Thickness(0, 4, 0, 0)
        };

        btnPickFile.Click += async (s, args) =>
        {
            try
            {
                var picked = await AppServices.Pickers.PickFileAsync(new[] { ".pdf", ".docx", ".doc", ".xlsx", ".xls", ".txt", ".jpg", ".png" });
                if (!string.IsNullOrWhiteSpace(picked))
                {
                    selectedSourcePath = picked;
                    lblChosenFile.Text = $"Selected: {System.IO.Path.GetFileName(picked)}";
                    lblChosenFile.Foreground = (Brush)Application.Current.Resources["AccentTextFillColorPrimaryBrush"];

                    if (string.IsNullOrWhiteSpace(txtTitle.Text))
                    {
                        txtTitle.Text = System.IO.Path.GetFileNameWithoutExtension(picked);
                    }
                }
            }
            catch (Exception ex)
            {
                ViewModel.ShowError($"File selection error: {ex.Message}");
            }
        };

        var dialog = new ContentDialog
        {
            XamlRoot = this.XamlRoot,
            Title = "New Template",
            Content = new ScrollViewer
            {
                Content = new StackPanel
                {
                    Spacing = 2,
                    Width = 420,
                    Children =
                    {
                        new TextBlock { Text = "Template File", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, FontSize = 12 },
                        btnPickFile,
                        lblChosenFile,

                        CreateRequiredHeader("Template / Form Title"),
                        txtTitle,

                        CreateRequiredHeader("Category"),
                        cmbCategory,

                        new TextBlock { Text = "Required Documents (comma-separated)", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, FontSize = 12 },
                        txtRequiredDocs,

                        new TextBlock { Text = "Operator Notes", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, FontSize = 12 },
                        txtNotes
                    }
                }
            },
            PrimaryButtonText = "Save",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary
        };

        var res = await dialog.ShowAsync();
        if (res == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(txtTitle.Text))
        {
            var category = !string.IsNullOrWhiteSpace(cmbCategory.Text) ? cmbCategory.Text.Trim() : (cmbCategory.SelectedItem?.ToString() ?? "Blank Forms");
            string? storedPath = null;
            string fileType = "PDF";
            string fileSize = "—";

            if (!string.IsNullOrWhiteSpace(selectedSourcePath) && System.IO.File.Exists(selectedSourcePath))
            {
                try
                {
                    storedPath = SevaDesk.Infrastructure.FileManager.TemplateStorageHelper.StoreTemplateFile(
                        selectedSourcePath, category, txtTitle.Text.Trim());
                    fileType = System.IO.Path.GetExtension(storedPath).TrimStart('.').ToUpperInvariant();
                    var fi = new System.IO.FileInfo(storedPath);
                    fileSize = SevaDesk.Infrastructure.FileManager.TemplateStorageHelper.FormatFileSize(fi.Length);
                }
                catch (Exception ex)
                {
                    ViewModel.ShowError($"Could not store template file: {ex.Message}");
                }
            }

            var newResource = new ResourceItem
            {
                Title = txtTitle.Text.Trim(),
                Category = category,
                FileType = fileType,
                FileSize = fileSize,
                FilePath = storedPath,
                RequiredDocs = txtRequiredDocs.Text.Trim(),
                Notes = txtNotes.Text.Trim(),
                Glyph = fileType == "PDF" ? "\uE8A5" : "\uE8C1",
                IsFavorite = false,
                LastModified = DateTime.UtcNow
            };

            await ViewModel.AddResourceAsync(newResource);
            UpdateRequiredDocsChips();
        }
    }
}
