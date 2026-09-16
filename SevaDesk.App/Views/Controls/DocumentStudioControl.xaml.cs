using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SevaDesk.Core.Interfaces;
using SevaDesk.Core.Models;
using SevaDesk_App.Services;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace SevaDesk_App.Views.Controls;

public sealed partial class DocumentStudioControl : UserControl
{
    private CanvasBitmap? _previewBitmap;
    private string? _currentEditingFile;
    public ObservableCollection<string> SourceFiles { get; } = new();

    public DocumentStudioControl()
    {
        this.InitializeComponent();
        SourceFilesList.ItemsSource = SourceFiles;
    }

    private ImageTransformModel GetCurrentConfig()
    {
        return new ImageTransformModel
        {
            Brightness = SldBrightness.Value,
            Contrast = SldContrast.Value,
            RotationAngle = SldRotation.Value,
            Grayscale = ChkGrayscale.IsChecked ?? false,
            Sharpen = ChkSharpen.IsChecked ?? false
        };
    }

    private async void UpdatePreview()
    {
        if (string.IsNullOrEmpty(_currentEditingFile)) return;

        try
        {
            LoadingRing.IsActive = true;
            var config = GetCurrentConfig();
            using var stream = await AppServices.ImageProcessing.GetPreviewStreamAsync(_currentEditingFile, config);
            _previewBitmap = await CanvasBitmap.LoadAsync(PreviewCanvas, stream.AsRandomAccessStream());
            PreviewCanvas.Invalidate(); // Trigger redrawing
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Preview Error: {ex.Message}");
        }
        finally
        {
            LoadingRing.IsActive = false;
        }
    }

    private void PreviewCanvas_Draw(CanvasControl sender, CanvasDrawEventArgs args)
    {
        if (_previewBitmap != null)
        {
            // Draw image centered and scaled to fit the canvas
            var canvasSize = sender.Size;
            var imgSize = _previewBitmap.Size;

            double scale = Math.Min(canvasSize.Width / imgSize.Width, canvasSize.Height / imgSize.Height);
            double w = imgSize.Width * scale;
            double h = imgSize.Height * scale;
            double x = (canvasSize.Width - w) / 2;
            double y = (canvasSize.Height - h) / 2;

            args.DrawingSession.DrawImage(_previewBitmap, new Windows.Foundation.Rect(x, y, w, h));
        }
    }

    private void Slider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        // Add a debounce in a real app, but for now just update directly
        UpdatePreview();
    }

    private void CheckBox_Changed(object sender, RoutedEventArgs e)
    {
        UpdatePreview();
    }

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        SldBrightness.Value = 0;
        SldContrast.Value = 1.0;
        SldRotation.Value = 0;
        ChkGrayscale.IsChecked = false;
        ChkSharpen.IsChecked = false;
        UpdatePreview();
    }

    private async void BrowseFile_Click(object sender, RoutedEventArgs e)
    {
        var file = await AppServices.Pickers.PickFileAsync([".jpg", ".png", ".jpeg", ".pdf", ".tiff"]);
        if (file != null)
        {
            SourceFiles.Add(file);
            SourceFilesList.SelectedIndex = SourceFiles.Count - 1;
        }
    }

    private void SourceFilesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SourceFilesList.SelectedItem is string selectedFile && !selectedFile.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            _currentEditingFile = selectedFile;
            TxtNoImage.Visibility = Visibility.Collapsed;
            UpdatePreview();
        }
    }

    private async void SaveImage_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_currentEditingFile)) return;

        var targetFolder = await AppServices.Pickers.PickFolderAsync();
        if (targetFolder == null) return;

        var ext = Path.GetExtension(_currentEditingFile);
        var baseName = Path.GetFileNameWithoutExtension(_currentEditingFile);
        var destPath = Path.Combine(targetFolder, $"{baseName}_edited{ext}");

        try
        {
            LoadingRing.IsActive = true;
            var config = GetCurrentConfig();
            await AppServices.ImageProcessing.ApplyTransformationsAsync(_currentEditingFile, destPath, config);
            await AppServices.Dialogs.ShowAlertAsync("Success", $"Saved to {destPath}");
        }
        catch (Exception ex)
        {
            await AppServices.Dialogs.ShowAlertAsync("Error", ex.Message);
        }
        finally
        {
            LoadingRing.IsActive = false;
        }
    }

    private async void GeneratePdf_Click(object sender, RoutedEventArgs e)
    {
        var files = SourceFiles.Where(f => !f.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)).ToList();
        if (files.Count == 0) return;

        var targetFolder = await AppServices.Pickers.PickFolderAsync();
        if (targetFolder == null) return;

        var destPath = Path.Combine(targetFolder, $"Document_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");

        var paperSize = CmbPaperSize.SelectedIndex switch
        {
            0 => PdfPaperSize.A4,
            1 => PdfPaperSize.A3,
            2 => PdfPaperSize.Letter,
            3 => PdfPaperSize.Legal,
            _ => PdfPaperSize.A4
        };

        var layoutMode = CmbLayoutMode.SelectedIndex switch
        {
            0 => PdfLayoutMode.SingleImageFit,
            1 => PdfLayoutMode.SingleImageFill,
            2 => PdfLayoutMode.MultipleImages,
            _ => PdfLayoutMode.SingleImageFit
        };

        try
        {
            LoadingRing.IsActive = true;
            await AppServices.PdfGeneration.GeneratePdfAsync(files, destPath, paperSize, layoutMode);
            await AppServices.Dialogs.ShowAlertAsync("Success", $"PDF Generated at {destPath}");
        }
        catch (Exception ex)
        {
            await AppServices.Dialogs.ShowAlertAsync("Error", ex.Message);
        }
        finally
        {
            LoadingRing.IsActive = false;
        }
    }

    private async void Print_Click(object sender, RoutedEventArgs e)
    {
        // To integrate properly with Windows Print APIs
        await AppServices.Dialogs.ShowAlertAsync("Printing", "Printing functionality via Windows Spooler will be connected here.");
    }
}
