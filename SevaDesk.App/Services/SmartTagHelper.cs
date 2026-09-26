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

    public static bool IsPhotoTag(string tagKey)

    {
        if (string.IsNullOrWhiteSpace(tagKey)) return false;
        var normalized = NormalizeTagKey(tagKey).ToLowerInvariant();
        return normalized == "photo" || normalized == "passport_photo" || normalized == "avatar" ||
               normalized.StartsWith("photo_") || normalized.EndsWith("_photo") ||
               normalized.Contains("passport") || normalized.Contains("profile_pic");
    }

    public static bool IsImageFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return false;
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        return ext is ".jpg" or ".jpeg" or ".png" or ".bmp" or ".webp";
    }

    public static async Task<bool> TryAssignCustomerPhotoAsync(Customer? customer, string photoPath)
    {
        if (customer == null || string.IsNullOrWhiteSpace(photoPath) || !File.Exists(photoPath))
            return false;

        if (!IsImageFile(photoPath))
            return false;

        try
        {
            customer.PhotoPath = photoPath;
            await AppServices.Customers.UpdatePhotoAsync(customer.Id, photoPath);
            CustomerAvatarHelper.NotifyAvatarUpdated(photoPath);
            return true;
        }

        catch
        {
            return false;
        }
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

        // 4. Saved Custom Tags
        var savedCustomTags = GetCachedCustomTags();
        if (savedCustomTags.Count > 0)
        {
            flyout.Items.Add(new MenuFlyoutSeparator());
            foreach (var cTag in savedCustomTags)
            {
                var item = new MenuFlyoutItem
                {
                    Text = cTag.DisplayName,
                    Tag = cTag.TagKey,
                    Icon = new FontIcon { Glyph = GetGlyphForTag(cTag.TagKey), Foreground = new SolidColorBrush(Color.FromArgb(255, 139, 92, 246)) }
                };
                item.Click += (s, e) => onTagSelected(cTag.TagKey);
                flyout.Items.Add(item);
            }

            var deleteSubMenu = new MenuFlyoutSubItem
            {
                Text = "Delete Saved Tag...",
                Icon = new FontIcon { Glyph = "\uE74D", Foreground = new SolidColorBrush(Color.FromArgb(255, 239, 68, 68)) }
            };
            foreach (var cTag in savedCustomTags)
            {
                var delItem = new MenuFlyoutItem
                {
                    Text = $"Delete '{cTag.DisplayName}'",
                    Icon = new FontIcon { Glyph = "\uE711" }
                };
                delItem.Click += async (s, e) =>
                {
                    await AppServices.CustomTags.DeleteAsync(cTag.TagKey);
                };
                deleteSubMenu.Items.Add(delItem);
            }
            flyout.Items.Add(deleteSubMenu);
        }

        flyout.Items.Add(new MenuFlyoutSeparator());

        // 5. New Custom Tag
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

    private static List<CustomDocumentTag>? _cachedCustomTags;
    private static readonly object _cacheLock = new();
    private static bool _isSubscribedToCustomTags;

    public static List<CustomDocumentTag> GetCachedCustomTags()
    {
        lock (_cacheLock)
        {
            if (!_isSubscribedToCustomTags)
            {
                _isSubscribedToCustomTags = true;
                AppServices.CustomTags.CustomTagsChanged += (s, e) =>
                {
                    lock (_cacheLock)
                    {
                        _cachedCustomTags = null;
                    }
                };
            }

            if (_cachedCustomTags == null)
            {
                try
                {
                    _cachedCustomTags = AppServices.CustomTags.GetAllAsync().GetAwaiter().GetResult().ToList();
                }
                catch
                {
                    _cachedCustomTags = [];
                }
            }

            return _cachedCustomTags.ToList();
        }
    }

    public static string FormatTagDisplayName(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "Document";
        var clean = Regex.Replace(raw.Trim(), @"[_\s\-]+", " ");
        var words = clean.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var capitalized = words.Select(w =>
        {
            if (string.Equals(w, "id", StringComparison.OrdinalIgnoreCase)) return "ID";
            if (string.Equals(w, "pan", StringComparison.OrdinalIgnoreCase)) return "PAN";
            return char.ToUpperInvariant(w[0]) + (w.Length > 1 ? w[1..].ToLowerInvariant() : "");
        });
        return string.Join(" ", capitalized);
    }

    public static async Task<CustomDocumentTag?> SaveCustomTagAsync(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var tagKey = NormalizeTagKey(raw);
        var displayName = FormatTagDisplayName(raw);
        var saved = await AppServices.CustomTags.CreateOrGetAsync(displayName, tagKey);
        lock (_cacheLock) { _cachedCustomTags = null; }
        return saved;
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
                        Text = "Enter document tag name (saved automatically for future use):",
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
            var raw = tb.Text.Trim();
            var saved = await SaveCustomTagAsync(raw);
            return saved?.TagKey;
        }

        return null;
    }

    public static string NormalizeTagKey(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "document";
        var clean = Regex.Replace(name.Trim().ToLowerInvariant(), @"[\s\-_/]+", "_");
        var invalidChars = Path.GetInvalidFileNameChars();
        clean = new string(clean.Where(c => !invalidChars.Contains(c)).ToArray());
        return string.IsNullOrWhiteSpace(clean) ? "document" : clean;
    }

    public static string GetGlyphForTag(string tagKey)
    {
        var key = tagKey.ToLowerInvariant();
        if (key.Contains("photo") || key.Contains("pic") || key.Contains("image")) return "\uEB9F";
        if (key.Contains("sign")) return "\uEDC6";
        if (key.Contains("aadhaar") || key.Contains("pan") || key.Contains("id") || key.Contains("voter")) return "\uE8D7";
        if (key.Contains("bank") || key.Contains("passbook") || key.Contains("acc")) return "\uE825";
        if (key.Contains("mark") || key.Contains("sheet") || key.Contains("result") || key.Contains("cert")) return "\uE7BE";
        return "\uE8A5";
    }

    private static readonly Dictionary<string, string[]> DocumentAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        { "aadhaar", new[] { "aadhaar", "adhar", "uidai", "aadhar" } },
        { "pan", new[] { "pan", "pancard", "pan_card" } },
        { "photo", new[] { "photo", "pic", "passport", "image", "avatar" } },
        { "sign", new[] { "sign", "signature", "hastakshar", "sign_eng", "sign_hindi" } },
        { "marksheet", new[] { "marksheet", "result", "10th", "12th", "matric", "inter", "diploma", "degree", "grad", "scorecard" } },
        { "voter", new[] { "voter", "epic", "election", "voter_id" } },
        { "ration", new[] { "ration", "rashan", "ration_card" } },
        { "bank", new[] { "bank", "passbook", "statement", "cheque", "bank_passbook" } },
        { "income", new[] { "income", "aay", "income_cert" } },
        { "caste", new[] { "caste", "jati", "caste_cert" } },
        { "domicile", new[] { "domicile", "niwas", "resident", "residence", "domicile_cert" } },
        { "cert", new[] { "cert", "certificate", "praman" } }
    };

    public static List<string> GetAliasesForTag(string tagKey)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { tagKey };
        var normalized = NormalizeTagKey(tagKey);
        result.Add(normalized);

        foreach (var (groupKey, aliases) in DocumentAliases)
        {
            if (string.Equals(groupKey, normalized, StringComparison.OrdinalIgnoreCase) ||
                aliases.Any(a => string.Equals(a, normalized, StringComparison.OrdinalIgnoreCase) || normalized.Contains(a)))
            {
                result.Add(groupKey);
                foreach (var a in aliases) result.Add(a);
            }
        }

        return result.ToList();
    }

    public static bool DoesAnyFileMatchTag(IEnumerable<string> filePaths, string tagKey)
    {
        var aliases = GetAliasesForTag(tagKey);
        foreach (var file in filePaths)
        {
            var nameOnly = Path.GetFileNameWithoutExtension(file).ToLowerInvariant();
            var cleanName = Regex.Replace(nameOnly, @"[\s\-]+", "_");

            foreach (var alias in aliases)
            {
                var cleanAlias = alias.ToLowerInvariant();
                if (cleanName.Equals(cleanAlias, StringComparison.OrdinalIgnoreCase) ||
                    cleanName.StartsWith($"{cleanAlias}_", StringComparison.OrdinalIgnoreCase) ||
                    cleanName.EndsWith($"_{cleanAlias}", StringComparison.OrdinalIgnoreCase) ||
                    cleanName.Contains($"_{cleanAlias}_", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }
        return false;
    }

    public static List<string> GetAllCustomerFilePaths(Customer? customer, string? folderPath)
    {
        var files = new List<string>();

        // 1. Working folder on Desktop (including subdirectories like 'Shared Docs' and scheme folders)
        if (!string.IsNullOrWhiteSpace(folderPath) && Directory.Exists(folderPath))
        {
            try
            {
                files.AddRange(Directory.GetFiles(folderPath, "*.*", SearchOption.AllDirectories));
            }
            catch { }
        }

        // 2. Customer Backup folder (permanent archive)
        try
        {
            string? backupFolder = null;
            if (customer != null && !string.IsNullOrWhiteSpace(customer.Name))
            {
                backupFolder = AppServices.FolderManager.GetCustomerBackupFolderPath(customer.Name, customer.Code);
            }
            else if (!string.IsNullOrWhiteSpace(folderPath))
            {
                var folderName = Path.GetFileName(folderPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                if (!string.IsNullOrWhiteSpace(folderName))
                {
                    backupFolder = AppServices.FolderManager.GetCustomerBackupFolderPath(folderName, string.Empty);
                }
            }

            if (!string.IsNullOrWhiteSpace(backupFolder) && Directory.Exists(backupFolder))
            {
                files.AddRange(Directory.GetFiles(backupFolder, "*.*", SearchOption.AllDirectories));
            }
        }
        catch { }

        return files;
    }

    public static bool CheckIfTagUsedForSession(ActiveSessionItem? session, string tagKey)
    {
        if (session == null) return false;
        var files = GetAllCustomerFilePaths(session.Customer, session.FolderPath);
        return DoesAnyFileMatchTag(files, tagKey);
    }

    public static List<DocumentTagItem> GetDocumentTagsForSessions(IReadOnlyList<ActiveSessionItem> sessions)
    {
        if (sessions == null || sessions.Count == 0)
        {
            return GetDocumentTags(null, null, null);
        }

        if (sessions.Count == 1)
        {
            return GetDocumentTagsForSession(sessions[0]);
        }

        var sessionFiles = sessions.ToDictionary(s => s, s => GetAllCustomerFilePaths(s.Customer, s.FolderPath));
        var primaryTags = new List<DocumentTagItem>();
        var customTags = new List<DocumentTagItem>();
        var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Required docs across sessions
        foreach (var s in sessions)
        {
            if (s.LinkedApplication != null && !string.IsNullOrWhiteSpace(s.LinkedApplication.RequiredDocs))
            {
                var parts = s.LinkedApplication.RequiredDocs.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                foreach (var part in parts)
                {
                    var tagKey = NormalizeTagKey(part);
                    if (seenKeys.Add(tagKey))
                    {
                        bool isUsedByAll = sessions.All(sess => DoesAnyFileMatchTag(sessionFiles[sess], tagKey));
                        primaryTags.Add(new DocumentTagItem(part, tagKey, GetGlyphForTag(tagKey), isUsedByAll, IsCustom: false));
                    }
                }
            }
        }

        // Standard defaults
        var defaults = new (string Name, string Key)[]
        {
            ("Photo", "photo"),
            ("Signature", "signature"),
            ("Aadhaar", "aadhaar"),
            ("PAN Card", "pan_card"),
            ("Bank Passbook", "bank_passbook"),
            ("Marksheet", "marksheet"),
            ("Certificate", "certificate")
        };

        foreach (var (name, key) in defaults)
        {
            if (seenKeys.Add(key))
            {
                bool isUsedByAll = sessions.All(sess => DoesAnyFileMatchTag(sessionFiles[sess], key));
                primaryTags.Add(new DocumentTagItem(name, key, GetGlyphForTag(key), isUsedByAll, IsCustom: false));
            }
        }

        // Saved Custom Tags
        var savedList = GetCachedCustomTags();
        foreach (var cTag in savedList)
        {
            if (seenKeys.Add(cTag.TagKey))
            {
                bool isUsedByAll = sessions.All(sess => DoesAnyFileMatchTag(sessionFiles[sess], cTag.TagKey));
                customTags.Add(new DocumentTagItem(cTag.DisplayName, cTag.TagKey, GetGlyphForTag(cTag.TagKey), isUsedByAll, IsCustom: true));
            }
        }

        var sortedPrimary = primaryTags.OrderBy(t => t.IsUsed ? 1 : 0).ToList();
        var sortedCustom = customTags.OrderBy(t => t.IsUsed ? 1 : 0).ToList();
        sortedPrimary.AddRange(sortedCustom);
        return sortedPrimary;
    }

    public static List<DocumentTagItem> GetDocumentTagsForSession(ActiveSessionItem? session)
    {
        if (session == null) return GetDocumentTags(null, null, null);
        return GetDocumentTags(session.FolderPath, session.LinkedApplication, session.Customer);
    }

    public static List<DocumentTagItem> GetDocumentTags(string? folderPath, ApplicationItem? linkedApp = null, Customer? customer = null)
    {
        var primaryTags = new List<DocumentTagItem>();
        var customTags = new List<DocumentTagItem>();
        var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var files = GetAllCustomerFilePaths(customer, folderPath);

        if (linkedApp != null && !string.IsNullOrWhiteSpace(linkedApp.RequiredDocs))
        {
            var parts = linkedApp.RequiredDocs.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                var tagKey = NormalizeTagKey(part);
                if (seenKeys.Add(tagKey))
                {
                    bool isUsed = DoesAnyFileMatchTag(files, tagKey);
                    primaryTags.Add(new DocumentTagItem(part, tagKey, GetGlyphForTag(tagKey), isUsed, IsCustom: false));
                }
            }
        }
        else
        {
            var defaults = new (string Name, string Key)[]
            {
                ("Photo", "photo"),
                ("Signature", "signature"),
                ("Aadhaar", "aadhaar"),
                ("PAN Card", "pan_card"),
                ("Bank Passbook", "bank_passbook"),
                ("Marksheet", "marksheet"),
                ("Certificate", "certificate")
            };

            foreach (var (name, key) in defaults)
            {
                if (seenKeys.Add(key))
                {
                    bool isUsed = DoesAnyFileMatchTag(files, key);
                    primaryTags.Add(new DocumentTagItem(name, key, GetGlyphForTag(key), isUsed, IsCustom: false));
                }
            }
        }

        // Saved Custom Tags
        var savedList = GetCachedCustomTags();
        foreach (var cTag in savedList)
        {
            if (seenKeys.Add(cTag.TagKey))
            {
                bool isUsed = DoesAnyFileMatchTag(files, cTag.TagKey);
                customTags.Add(new DocumentTagItem(cTag.DisplayName, cTag.TagKey, GetGlyphForTag(cTag.TagKey), isUsed, IsCustom: true));
            }
        }

        var sortedPrimary = primaryTags.OrderBy(t => t.IsUsed ? 1 : 0).ToList();
        var sortedCustom = customTags.OrderBy(t => t.IsUsed ? 1 : 0).ToList();

        // Placed after standard/required tags, before [+ Custom...]
        sortedPrimary.AddRange(sortedCustom);
        return sortedPrimary;
    }

    public static bool CheckIfTagUsedInFolder(string? folderPath, string tagKey)
    {
        var files = GetAllCustomerFilePaths(null, folderPath);
        return DoesAnyFileMatchTag(files, tagKey);
    }
}

public record DocumentTagItem(string DisplayName, string TagKey, string Glyph, bool IsUsed, bool IsCustom = false);

