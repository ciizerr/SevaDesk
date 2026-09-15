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

    private void AddNewResource_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.StatusMessage = "Template import: Select any PDF or Word template from disk.";
    }

    private void CategoryAll_Click(object sender, RoutedEventArgs e) => ViewModel.SetCategoryCommand.Execute("All");
    private void CategoryAffidavits_Click(object sender, RoutedEventArgs e) => ViewModel.SetCategoryCommand.Execute("Affidavits");
    private void CategoryForms_Click(object sender, RoutedEventArgs e) => ViewModel.SetCategoryCommand.Execute("Blank Forms");
    private void CategoryGuidelines_Click(object sender, RoutedEventArgs e) => ViewModel.SetCategoryCommand.Execute("Guidelines");

    private void ToggleFavorite_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is ResourceItem item)
        {
            ViewModel.ToggleFavoriteCommand.Execute(item);
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

    private void InspectorCopyToFolder_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedResource != null)
        {
            ViewModel.CopyToActiveSessionCommand.Execute(ViewModel.SelectedResource);
        }
    }
}
