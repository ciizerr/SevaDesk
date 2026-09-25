using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using SevaDesk.Core.Models;
using SevaDesk_App.Services;
using Windows.Data.Pdf;
using Windows.Storage;
using Windows.Storage.Streams;

namespace SevaDesk_App.Views.Dialogs;

public sealed partial class FilePreviewDialog : ContentDialog
{
    private readonly FolderFileItem _fileItem;

    public FilePreviewDialog(FolderFileItem fileItem)
    {
        InitializeComponent();
        this.EnableLightDismiss();
        _fileItem = fileItem;

        TxtFileName.Text = _fileItem.Name;
        TxtFileSize.Text = _fileItem.FormattedSize;
        TxtFileModified.Text = _fileItem.FormattedDate;
        TxtFormat.Text = string.IsNullOrEmpty(_fileItem.Extension) ? "FILE" : _fileItem.Extension.ToUpperInvariant().TrimStart('.');
        TxtFullPath.Text = _fileItem.FullPath;
        FileGlyphIcon.Glyph = _fileItem.Glyph;

        PrimaryButtonClick += (s, e) => AppServices.FolderManager.OpenFileWithDefaultApp(_fileItem.FullPath);
        SecondaryButtonClick += (s, e) =>
        {
            var dir = Path.GetDirectoryName(_fileItem.FullPath);
            if (!string.IsNullOrEmpty(dir))
            {
                AppServices.FolderManager.OpenFolderInExplorer(dir);
            }
        };

        Loaded += async (s, e) => await LoadPreviewAsync();
    }

    private async Task LoadPreviewAsync()
    {
        PreviewProgress.IsActive = true;
        PreviewProgress.Visibility = Visibility.Visible;
        GenericDocFallback.Visibility = Visibility.Collapsed;

        try
        {
            if (!File.Exists(_fileItem.FullPath))
            {
                ShowFallback("File Not Found");
                return;
            }

            if (_fileItem.IsImage)
            {
                var bitmap = new BitmapImage();
                bitmap.UriSource = new Uri(_fileItem.FullPath);
                PreviewImage.Source = bitmap;
                PreviewProgress.IsActive = false;
                PreviewProgress.Visibility = Visibility.Collapsed;
            }
            else if (_fileItem.IsPdf)
            {
                await LoadPdfPreviewAsync();
            }
            else
            {
                ShowFallback(_fileItem.Name);
            }
        }
        catch (Exception ex)
        {
            ShowFallback($"Preview Unavailable: {ex.Message}");
        }
    }

    private async Task LoadPdfPreviewAsync()
    {
        try
        {
            var storageFile = await StorageFile.GetFileFromPathAsync(_fileItem.FullPath);
            var pdfDoc = await PdfDocument.LoadFromFileAsync(storageFile);
            if (pdfDoc.PageCount > 0)
            {
                using var page = pdfDoc.GetPage(0);
                using var stream = new InMemoryRandomAccessStream();
                await page.RenderToStreamAsync(stream);

                var bitmap = new BitmapImage();
                await bitmap.SetSourceAsync(stream);
                PreviewImage.Source = bitmap;
                PreviewProgress.IsActive = false;
                PreviewProgress.Visibility = Visibility.Collapsed;
            }
            else
            {
                ShowFallback("Empty PDF Document");
            }
        }
        catch
        {
            ShowFallback(_fileItem.Name);
        }
    }

    private void ShowFallback(string title)
    {
        PreviewProgress.IsActive = false;
        PreviewProgress.Visibility = Visibility.Collapsed;
        PreviewImage.Source = null;
        GenericDocFallback.Visibility = Visibility.Visible;
        FallbackGlyph.Glyph = _fileItem.Glyph;
        TxtFallbackTitle.Text = title;
    }
}
