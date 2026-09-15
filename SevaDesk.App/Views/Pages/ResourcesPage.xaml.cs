using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using SevaDesk_App.ViewModels.Pages;
using SevaDesk.Core.Models;

namespace SevaDesk_App.Views.Pages;

public sealed partial class ResourcesPage : Page
{
    public ResourcesViewModel ViewModel { get; } = new();

    public ResourcesPage()
    {
        InitializeComponent();
    }

    public static SolidColorBrush FavoriteBrush(bool isFav) =>
        isFav ? new SolidColorBrush(ColorHelper.FromArgb(255, 245, 158, 11)) : new SolidColorBrush(ColorHelper.FromArgb(120, 128, 128, 128));

    public static bool HasStatus(string status) => !string.IsNullOrWhiteSpace(status);

    private async void AddNewResource_Click(object sender, RoutedEventArgs e)
    {
        var txtTitle = new TextBox { PlaceholderText = "e.g. Domicile Affidavit 2026", Margin = new Thickness(0, 4, 0, 8) };
        var cmbCategory = new ComboBox
        {
            ItemsSource = new[] { "Affidavits", "Blank Forms", "Guidelines" },
            SelectedIndex = 0,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(0, 4, 0, 8)
        };
        var txtFileType = new TextBox { Text = "DOCX", Margin = new Thickness(0, 4, 0, 0) };

        var dialog = new ContentDialog
        {
            XamlRoot = this.XamlRoot,
            Title = "Add Offline Template or Form",
            Content = new StackPanel
            {
                Spacing = 4,
                Children =
                {
                    new TextBlock { Text = "Template / Form Title *", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold },
                    txtTitle,
                    new TextBlock { Text = "Category", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold },
                    cmbCategory,
                    new TextBlock { Text = "File Type (e.g. PDF, DOCX)", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold },
                    txtFileType
                }
            },
            PrimaryButtonText = "Add to Catalog",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary
        };

        var res = await dialog.ShowAsync();
        if (res == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(txtTitle.Text))
        {
            var category = cmbCategory.SelectedItem?.ToString() ?? "Affidavits";
            var fileType = string.IsNullOrWhiteSpace(txtFileType.Text) ? "PDF" : txtFileType.Text.Trim().ToUpperInvariant();
            var glyph = fileType == "PDF" ? "\uE8A5" : "\uE8C1";

            var newResource = new ResourceItem
            {
                Title = txtTitle.Text.Trim(),
                Category = category,
                FileType = fileType,
                FileSize = "50 KB",
                Glyph = glyph,
                IsFavorite = false,
                LastModified = DateTime.UtcNow
            };

            await ViewModel.AddResourceAsync(newResource);
        }
    }

    private void CategoryAll_Click(object sender, RoutedEventArgs e) => ViewModel.SetCategoryCommand.Execute("All");
    private void CategoryAffidavits_Click(object sender, RoutedEventArgs e) => ViewModel.SetCategoryCommand.Execute("Affidavits");
    private void CategoryForms_Click(object sender, RoutedEventArgs e) => ViewModel.SetCategoryCommand.Execute("Blank Forms");
    private void CategoryGuidelines_Click(object sender, RoutedEventArgs e) => ViewModel.SetCategoryCommand.Execute("Guidelines");

    private async void ToggleFavorite_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is ResourceItem item)
        {
            await ViewModel.ToggleFavoriteAsync(item);
        }
    }

    private void QuickPrint_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is ResourceItem item)
        {
            ViewModel.QuickPrintCommand.Execute(item);
        }
    }

    private void InspectorPrint_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedResource != null)
        {
            ViewModel.QuickPrintCommand.Execute(ViewModel.SelectedResource);
        }
    }

    private async void InspectorCopyToFolder_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedResource != null)
        {
            await ViewModel.CopyToActiveSessionAsync(ViewModel.SelectedResource);
        }
    }
}
