using System.IO;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using SevaDesk.Core.Models;

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

    public async Task<(DeleteSessionFileAction Action, int FileCount)> PromptDeleteActiveSessionAsync(ActiveSessionItem item, XamlRoot? xamlRoot = null)
    {
        var custName = item.Customer?.Name ?? "this customer";
        int fileCount = 0;

        if (item.Customer != null)
        {
            var workingFolder = AppServices.FolderManager.GetCustomerFolderPath(item.Customer.Name, item.Customer.Code);
            if (!string.IsNullOrWhiteSpace(workingFolder) && Directory.Exists(workingFolder))
            {
                try
                {
                    fileCount = Directory.GetFiles(workingFolder, "*", SearchOption.AllDirectories).Length;
                }
                catch { }
            }
        }

        if (fileCount == 0)
        {
            // Simple confirmation dialog since there are 0 files
            var panel = new StackPanel { Spacing = 10, Width = 440 };
            panel.Children.Add(new TextBlock
            {
                Text = $"Delete session for '{custName}'? Action cannot be undone.",
                TextWrapping = TextWrapping.Wrap,
                FontSize = 13
            });

            if (item.LinkedApplication != null && item.LinkedApplication.Status != "Completed")
            {
                panel.Children.Add(new TextBlock
                {
                    Text = $"Note: Linked draft application '{item.LinkedApplication.Title}' will also be deleted.",
                    FontSize = 12,
                    Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                    TextWrapping = TextWrapping.Wrap
                });
            }

            var dialog = new ContentDialog
            {
                Title = "Delete Session",
                Content = panel,
                PrimaryButtonText = "Delete",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close
            };
            if (xamlRoot != null) dialog.XamlRoot = xamlRoot;
            else EnsureXamlRoot(dialog);

            var res = await dialog.ShowAsync();
            return (res == ContentDialogResult.Primary ? DeleteSessionFileAction.KeepOnDesktop : DeleteSessionFileAction.Cancel, 0);
        }
        else
        {
            // 3-action dialog with rich explanations and 460 DIP width
            var panel = new StackPanel { Spacing = 12, Width = 460 };

            var alertBorder = new Border
            {
                Background = (Brush)Application.Current.Resources["SubtleFillColorSecondaryBrush"],
                BorderBrush = (Brush)Application.Current.Resources["SystemFillColorCautionBrush"],
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
                FontSize = 18,
                Foreground = (Brush)Application.Current.Resources["SystemFillColorCautionBrush"],
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
                        Text = $"Active Session: {custName}",
                        FontWeight = FontWeights.SemiBold,
                        FontSize = 13
                    },
                    new TextBlock
                    {
                        Text = $"Working folder has {fileCount} file{(fileCount == 1 ? "" : "s")}. Choose file action before deleting session:",
                        FontSize = 12,
                        Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                        TextWrapping = TextWrapping.Wrap
                    }
                }
            };

            if (item.LinkedApplication != null && item.LinkedApplication.Status != "Completed")
            {
                headerStack.Children.Add(new TextBlock
                {
                    Text = $"Note: Linked draft application '{item.LinkedApplication.Title}' will also be deleted.",
                    FontSize = 11,
                    Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                    TextWrapping = TextWrapping.Wrap
                });
            }

            Grid.SetColumn(headerStack, 1);
            grid.Children.Add(headerStack);
            alertBorder.Child = grid;

            panel.Children.Add(alertBorder);

            var cardStack = new StackPanel { Spacing = 8, Margin = new Thickness(0, 2, 0, 0) };

            // Backup option card
            var backupCard = new Border
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
                                new FontIcon { Glyph = "\uE8B7", FontSize = 12, Foreground = (Brush)Application.Current.Resources["AccentTextFillColorPrimaryBrush"] },
                                new TextBlock { Text = "Backup & Delete", FontWeight = FontWeights.SemiBold, FontSize = 12 }
                            }
                        },
                        new TextBlock
                        {
                            Text = $"Copies all {fileCount} file{(fileCount == 1 ? "" : "s")} to customer backup archive and removes folder from Desktop.",
                            FontSize = 11,
                            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                            TextWrapping = TextWrapping.Wrap
                        }
                    }
                }
            };
            cardStack.Children.Add(backupCard);

            // Keep on desktop option card
            var keepCard = new Border
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
                                new FontIcon { Glyph = "\uE7F4", FontSize = 12 },
                                new TextBlock { Text = "Keep on Desktop", FontWeight = FontWeights.SemiBold, FontSize = 12 }
                            }
                        },
                        new TextBlock
                        {
                            Text = "Leaves folder on Desktop and removes session record.",
                            FontSize = 11,
                            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                            TextWrapping = TextWrapping.Wrap
                        }
                    }
                }
            };
            cardStack.Children.Add(keepCard);

            panel.Children.Add(cardStack);

            var dialog = new ContentDialog
            {
                Title = "Delete Session",
                Content = panel,
                PrimaryButtonText = "Backup & Delete",
                SecondaryButtonText = "Keep on Desktop",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary
            };
            if (xamlRoot != null) dialog.XamlRoot = xamlRoot;
            else EnsureXamlRoot(dialog);

            var res = await dialog.ShowAsync();
            DeleteSessionFileAction action = res switch
            {
                ContentDialogResult.Primary => DeleteSessionFileAction.BackupAndDelete,
                ContentDialogResult.Secondary => DeleteSessionFileAction.KeepOnDesktop,
                _ => DeleteSessionFileAction.Cancel
            };

            return (action, fileCount);
        }
    }

    public async Task<DeleteCustomerAction> PromptDeleteCustomerAsync(Customer customer, XamlRoot? xamlRoot = null)
    {
        var panel = new StackPanel { Spacing = 12, Width = 460 };

        var alertBorder = new Border
        {
            Background = (Brush)Application.Current.Resources["SubtleFillColorSecondaryBrush"],
            BorderBrush = (Brush)Application.Current.Resources["SystemFillColorCautionBrush"],
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
            FontSize = 18,
            Foreground = (Brush)Application.Current.Resources["SystemFillColorCautionBrush"],
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
                    Text = $"Delete Customer: {customer.Name} ({customer.Code})",
                    FontWeight = FontWeights.SemiBold,
                    FontSize = 13
                },
                new TextBlock
                {
                    Text = "Permanently deletes profile, sessions, and forms. Financial invoices are preserved for bookkeeping.",
                    FontSize = 12,
                    Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                    TextWrapping = TextWrapping.Wrap
                }
            }
        };
        Grid.SetColumn(headerStack, 1);
        grid.Children.Add(headerStack);
        alertBorder.Child = grid;

        panel.Children.Add(alertBorder);

        var cardStack = new StackPanel { Spacing = 8, Margin = new Thickness(0, 2, 0, 0) };

        // 1. Delete & Keep Backup
        var backupCard = new Border
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
                            new FontIcon { Glyph = "\uE8B7", FontSize = 12, Foreground = (Brush)Application.Current.Resources["AccentTextFillColorPrimaryBrush"] },
                            new TextBlock { Text = "Delete & Keep Backup", FontWeight = FontWeights.SemiBold, FontSize = 12 }
                        }
                    },
                    new TextBlock
                    {
                        Text = "Deletes profile and cleans Desktop, keeping files safe in backup archive.",
                        FontSize = 11,
                        Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                        TextWrapping = TextWrapping.Wrap
                    }
                }
            }
        };
        cardStack.Children.Add(backupCard);

        // 2. Delete & Wipe All Files
        var wipeCard = new Border
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
                            new FontIcon { Glyph = "\uE74D", FontSize = 12, Foreground = (Brush)Application.Current.Resources["SystemFillColorCriticalBrush"] },
                            new TextBlock { Text = "Delete & Wipe Files", FontWeight = FontWeights.SemiBold, FontSize = 12 }
                        }
                    },
                    new TextBlock
                    {
                        Text = "Permanently wipes customer files from both Desktop and backup archive.",
                        FontSize = 11,
                        Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                        TextWrapping = TextWrapping.Wrap
                    }
                }
            }
        };
        cardStack.Children.Add(wipeCard);

        panel.Children.Add(cardStack);

        var dialog = new ContentDialog
        {
            Title = "Delete Customer",
            Content = panel,
            PrimaryButtonText = "Delete & Backup",
            SecondaryButtonText = "Delete & Wipe",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close
        };
        if (xamlRoot != null) dialog.XamlRoot = xamlRoot;
        else EnsureXamlRoot(dialog);

        var res = await dialog.ShowAsync();
        return res switch
        {
            ContentDialogResult.Primary => DeleteCustomerAction.DeleteAndKeepBackup,
            ContentDialogResult.Secondary => DeleteCustomerAction.DeleteAndWipeAll,
            _ => DeleteCustomerAction.Cancel
        };
    }
}
