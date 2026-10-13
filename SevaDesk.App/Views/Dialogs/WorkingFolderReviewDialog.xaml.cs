using System;
using System.Collections.Generic;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using SevaDesk.Core.Models;
using SevaDesk_App.Services;

namespace SevaDesk_App.Views.Dialogs;

public enum ReviewFolderChoice
{
    UpdateBackupAndClean = 0,
    KeepOnDesktop = 1,
    DeleteWithoutBackup = 2
}

public class ChangedFolderCandidate
{
    public string SessionId { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerCode { get; set; } = string.Empty;
    public string WorkingFolderPath { get; set; } = string.Empty;
    public FolderDiffResult Diff { get; set; } = new();
    public ReviewFolderChoice ChosenAction { get; set; } = ReviewFolderChoice.UpdateBackupAndClean;
}

public class DiffItemDisplayModel
{
    public string Glyph { get; set; } = "\uE8A5";
    public SolidColorBrush GlyphColorBrush { get; set; } = new(Colors.Gray);
    public string RelativePath { get; set; } = string.Empty;
    public string SizeText { get; set; } = string.Empty;
}

public sealed partial class WorkingFolderReviewDialog : ContentDialog
{
    public ChangedFolderCandidate Candidate { get; }
    public ReviewFolderChoice ResultChoice { get; private set; } = ReviewFolderChoice.KeepOnDesktop;

    public WorkingFolderReviewDialog(ChangedFolderCandidate candidate, int currentIndex = 0, int totalCount = 1)
    {
        InitializeComponent();
        this.EnableLightDismiss();

        Candidate = candidate;

        if (totalCount > 1)
        {
            Title = $"Review Modified Folder ({currentIndex + 1} of {totalCount})";
        }
        else
        {
            Title = "Review Modified Customer Folder";
        }

        TxtCustomerDisplayName.Text = $"{candidate.CustomerName} ({candidate.CustomerCode})";
        TxtWorkingFolderPath.Text = candidate.WorkingFolderPath;

        // Badges setup
        BadgeAdded.Visibility = candidate.Diff.AddedCount > 0 ? Visibility.Visible : Visibility.Collapsed;
        TxtAddedCount.Text = $"+{candidate.Diff.AddedCount} Added";

        BadgeModified.Visibility = candidate.Diff.ModifiedCount > 0 ? Visibility.Visible : Visibility.Collapsed;
        TxtModifiedCount.Text = $"{candidate.Diff.ModifiedCount} Modified";

        BadgeDeleted.Visibility = candidate.Diff.DeletedCount > 0 ? Visibility.Visible : Visibility.Collapsed;
        TxtDeletedCount.Text = $"-{candidate.Diff.DeletedCount} Deleted";

        DiffExpander.Header = $"Changed files ({candidate.Diff.TotalChangesCount})";

        var diffItems = new List<DiffItemDisplayModel>();

        foreach (var item in candidate.Diff.AddedFiles)
        {
            diffItems.Add(new DiffItemDisplayModel
            {
                Glyph = "\uE710", // Plus
                GlyphColorBrush = new SolidColorBrush(ColorHelper.FromArgb(255, 16, 185, 129)), // Emerald green
                RelativePath = $"+ {item.RelativePath}",
                SizeText = FormatSize(item.FileSizeBytes)
            });
        }

        foreach (var item in candidate.Diff.ModifiedFiles)
        {
            diffItems.Add(new DiffItemDisplayModel
            {
                Glyph = "\uE70F", // Edit pencil
                GlyphColorBrush = new SolidColorBrush(ColorHelper.FromArgb(255, 245, 158, 11)), // Amber
                RelativePath = $"~ {item.RelativePath}",
                SizeText = FormatSize(item.FileSizeBytes)
            });
        }

        foreach (var item in candidate.Diff.DeletedFiles)
        {
            diffItems.Add(new DiffItemDisplayModel
            {
                Glyph = "\uE74D", // Trash / delete
                GlyphColorBrush = new SolidColorBrush(ColorHelper.FromArgb(255, 239, 68, 68)), // Red
                RelativePath = $"- {item.RelativePath}",
                SizeText = "Deleted"
            });
        }

        DiffItemsControl.ItemsSource = diffItems;

        PrimaryButtonClick += (s, e) =>
        {
            ResultChoice = ReviewFolderChoice.UpdateBackupAndClean;
        };

        SecondaryButtonClick += (s, e) =>
        {
            ResultChoice = ReviewFolderChoice.DeleteWithoutBackup;
        };

        CloseButtonClick += (s, e) =>
        {
            ResultChoice = ReviewFolderChoice.KeepOnDesktop;
        };
    }

    private static string FormatSize(long bytes)
    {
        if (bytes <= 0) return string.Empty;
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        return $"{bytes / (1024.0 * 1024.0):F1} MB";
    }
}
