using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace SevaDesk_App.Services;

public class DialogService : IDialogService
{
    private static XamlRoot? _globalXamlRoot;

    public static void Initialize(XamlRoot root)
    {
        _globalXamlRoot = root;
    }

    private void EnsureXamlRoot(ContentDialog dialog)
    {
        if (_globalXamlRoot == null)
            throw new InvalidOperationException("DialogService has not been initialized with a XamlRoot. Call DialogService.Initialize() from the MainWindow.");
        
        dialog.XamlRoot = _globalXamlRoot;
    }

    public async Task<ContentDialogResult> ShowConfirmationAsync(string title, string content, string primaryButtonText = "Yes", string secondaryButtonText = "No")
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = new TextBlock { Text = content, TextWrapping = TextWrapping.Wrap },
            PrimaryButtonText = primaryButtonText,
            SecondaryButtonText = secondaryButtonText,
            DefaultButton = ContentDialogButton.Primary
        };
        EnsureXamlRoot(dialog);
        return await dialog.ShowAsync();
    }

    public async Task<(ContentDialogResult Result, string? Input)> ShowInputAsync(string title, string content, string placeholderText = "", string primaryButtonText = "OK", string secondaryButtonText = "Cancel")
    {
        var input = new TextBox
        {
            PlaceholderText = placeholderText,
            Margin = new Thickness(0, 4, 0, 0)
        };
        
        var panel = new StackPanel { Spacing = 10 };
        if (!string.IsNullOrWhiteSpace(content))
        {
            panel.Children.Add(new TextBlock { Text = content, TextWrapping = TextWrapping.Wrap });
        }
        panel.Children.Add(input);

        var dialog = new ContentDialog
        {
            Title = title,
            Content = panel,
            PrimaryButtonText = primaryButtonText,
            SecondaryButtonText = secondaryButtonText,
            DefaultButton = ContentDialogButton.Primary
        };
        EnsureXamlRoot(dialog);
        
        var result = await dialog.ShowAsync();
        return (result, input.Text);
    }

    public async Task ShowAlertAsync(string title, string content, string closeButtonText = "OK")
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = new TextBlock { Text = content, TextWrapping = TextWrapping.Wrap },
            CloseButtonText = closeButtonText
        };
        EnsureXamlRoot(dialog);
        await dialog.ShowAsync();
    }

    public async Task<ContentDialogResult> ShowCustomDialogAsync(ContentDialog dialog)
    {
        EnsureXamlRoot(dialog);
        return await dialog.ShowAsync();
    }
}
