using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using SevaDesk.Core.Models;
using Windows.UI;

namespace SevaDesk_App.Services;

public static class SmartTagHelper
{
    public static string GenerateUniqueFileName(string directory, string baseName, string extension)
    {
        var cleanExt = extension.StartsWith('.') ? extension : $".{extension}";
        var cleanBase = baseName.Trim().ToLowerInvariant();

        var targetName = $"{cleanBase}{cleanExt}";
        var targetPath = Path.Combine(directory, targetName);
        if (!File.Exists(targetPath)) return targetName;

        for (int counter = 2; counter < 1000; counter++)
        {
            targetName = $"{cleanBase}_{counter}{cleanExt}";
            targetPath = Path.Combine(directory, targetName);
            if (!File.Exists(targetPath)) return targetName;
        }

        return $"{cleanBase}_{Guid.NewGuid().ToString("N")[..4]}{cleanExt}";
    }

    public static MenuFlyout CreateTagFlyout(FolderFileItem file, Action<string> onTagSelected)
    {
        var flyout = new MenuFlyout();

        // 1. Customer Photo
        var photoItem = new MenuFlyoutItem
        {
            Text = "Customer Photo (Profile)",
            Tag = "photo",
            Icon = new FontIcon { Glyph = "\uEB9F", Foreground = new SolidColorBrush(Color.FromArgb(255, 59, 130, 246)) }
        };
        photoItem.Click += (s, e) => onTagSelected("photo");
        flyout.Items.Add(photoItem);

        flyout.Items.Add(new MenuFlyoutSeparator());

        // 2. Signatures
        var signEngItem = new MenuFlyoutItem
        {
            Text = "Signature (English)",
            Tag = "sign_eng",
            Icon = new FontIcon { Glyph = "\uEDC6", Foreground = new SolidColorBrush(Color.FromArgb(255, 16, 185, 129)) }
        };
        signEngItem.Click += (s, e) => onTagSelected("sign_eng");
        flyout.Items.Add(signEngItem);

        var signHindiItem = new MenuFlyoutItem
        {
            Text = "Signature (Hindi)",
            Tag = "sign_hindi",
            Icon = new FontIcon { Glyph = "\uEDC6", Foreground = new SolidColorBrush(Color.FromArgb(255, 5, 150, 105)) }
        };
        signHindiItem.Click += (s, e) => onTagSelected("sign_hindi");
        flyout.Items.Add(signHindiItem);

        flyout.Items.Add(new MenuFlyoutSeparator());

        // 3. Identity & Documents
        var aadhaarItem = new MenuFlyoutItem
        {
            Text = "Aadhaar Card",
            Tag = "aadhaar",
            Icon = new FontIcon { Glyph = "\uE8D7", Foreground = new SolidColorBrush(Color.FromArgb(255, 245, 158, 11)) }
        };
        aadhaarItem.Click += (s, e) => onTagSelected("aadhaar");
        flyout.Items.Add(aadhaarItem);

        var panItem = new MenuFlyoutItem
        {
            Text = "PAN Card",
            Tag = "pan_card",
            Icon = new FontIcon { Glyph = "\uE8D7", Foreground = new SolidColorBrush(Color.FromArgb(255, 139, 92, 246)) }
        };
        panItem.Click += (s, e) => onTagSelected("pan_card");
        flyout.Items.Add(panItem);

        var marksheetItem = new MenuFlyoutItem
        {
            Text = "Marksheet / Result",
            Tag = "marksheet",
            Icon = new FontIcon { Glyph = "\uE7BE", Foreground = new SolidColorBrush(Color.FromArgb(255, 99, 102, 241)) }
        };
        marksheetItem.Click += (s, e) => onTagSelected("marksheet");
        flyout.Items.Add(marksheetItem);

        var bankItem = new MenuFlyoutItem
        {
            Text = "Bank Passbook",
            Tag = "bank_passbook",
            Icon = new FontIcon { Glyph = "\uE825", Foreground = new SolidColorBrush(Color.FromArgb(255, 14, 165, 233)) }
        };
        bankItem.Click += (s, e) => onTagSelected("bank_passbook");
        flyout.Items.Add(bankItem);

        var certItem = new MenuFlyoutItem
        {
            Text = "Certificate (Caste/Income/Domicile)",
            Tag = "certificate",
            Icon = new FontIcon { Glyph = "\uE8A5", Foreground = new SolidColorBrush(Color.FromArgb(255, 236, 72, 153)) }
        };
        certItem.Click += (s, e) => onTagSelected("certificate");
        flyout.Items.Add(certItem);

        flyout.Items.Add(new MenuFlyoutSeparator());

        // 4. Custom
        var customItem = new MenuFlyoutItem
        {
            Text = "Custom Tag...",
            Tag = "custom",
            Icon = new FontIcon { Glyph = "\uE70F" }
        };
        customItem.Click += (s, e) => onTagSelected("custom");
        flyout.Items.Add(customItem);

        return flyout;
    }

    public static async Task<string?> PromptCustomTagAsync(XamlRoot xamlRoot)
    {
        var tb = new TextBox
        {
            PlaceholderText = "e.g. voter_id, ration_card, affidavit",
            Margin = new Thickness(0, 10, 0, 0)
        };
        var dialog = new ContentDialog
        {
            Title = "Custom Document Tag",
            Content = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    new TextBlock
                    {
                        Text = "Enter document tag name (spaces will convert to underscores):",
                        TextWrapping = TextWrapping.Wrap
                    },
                    tb
                }
            },
            PrimaryButtonText = "Tag & Rename",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = xamlRoot
        };

        var res = await dialog.ShowAsync();
        if (res == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(tb.Text))
        {
            var cleanTag = Regex.Replace(tb.Text.Trim(), @"\s+", "_").ToLowerInvariant();
            var invalidChars = Path.GetInvalidFileNameChars();
            cleanTag = new string(cleanTag.Where(c => !invalidChars.Contains(c)).ToArray());
            return string.IsNullOrWhiteSpace(cleanTag) ? null : cleanTag;
        }

        return null;
    }
}
