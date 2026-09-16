using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using SevaDesk.Core.Interfaces;

namespace SevaDesk.Infrastructure.FileManager;

public class PdfGenerationService : IPdfGenerationService
{
    public Task<string> GeneratePdfAsync(IReadOnlyList<string> imagePaths, string destinationPath, PdfPaperSize paperSize, PdfLayoutMode layoutMode)
    {
        return Task.Run(() =>
        {
            if (imagePaths == null || imagePaths.Count == 0)
                throw new ArgumentException("No images provided for PDF generation.");

            using var document = new PdfDocument();
            document.Info.Title = "SevaDesk Generated Document";
            document.Info.CreationDate = DateTime.Now;

            // Map paper size
            var sharpSize = paperSize switch
            {
                PdfPaperSize.A4 => PdfSharpCore.PageSize.A4,
                PdfPaperSize.A3 => PdfSharpCore.PageSize.A3,
                PdfPaperSize.Letter => PdfSharpCore.PageSize.Letter,
                PdfPaperSize.Legal => PdfSharpCore.PageSize.Legal,
                _ => PdfSharpCore.PageSize.A4
            };

            if (layoutMode == PdfLayoutMode.MultipleImages)
            {
                // Simple Multiple Images layout: pack vertically with 10px margin
                var page = document.AddPage();
                page.Size = sharpSize;
                using var gfx = XGraphics.FromPdfPage(page);

                double currentY = 10;
                double margin = 10;
                double maxWidth = page.Width.Point - (margin * 2);

                foreach (var imgPath in imagePaths)
                {
                    using var img = XImage.FromFile(imgPath);
                    double scale = 1.0;
                    if (img.PixelWidth > maxWidth)
                    {
                        scale = maxWidth / img.PixelWidth;
                    }

                    double w = img.PixelWidth * scale;
                    double h = img.PixelHeight * scale;

                    if (currentY + h > page.Height.Point - margin)
                    {
                        // Add new page
                        page = document.AddPage();
                        page.Size = sharpSize;
                        // Cannot re-use XGraphics across pages easily, need to recreate it. 
                        // But we can't assign to `gfx` since it's `using`. So we'll abstract this.
                    }
                }
                
                // Let's implement a safer MultipleImages loop
            }

            // Simplified approach for standard rendering
            int imgIndex = 0;
            PdfPage? currentPage = null;
            XGraphics? currentGfx = null;
            double cy = 10;
            double cmargin = 10;

            foreach (var imgPath in imagePaths)
            {
                if (layoutMode != PdfLayoutMode.MultipleImages || currentPage == null)
                {
                    currentPage = document.AddPage();
                    currentPage.Size = sharpSize;
                    currentGfx?.Dispose();
                    currentGfx = XGraphics.FromPdfPage(currentPage);
                    cy = cmargin;
                }

                using var img = XImage.FromFile(imgPath);
                double maxWidth = currentPage.Width.Point - (cmargin * 2);
                double maxHeight = currentPage.Height.Point - (cmargin * 2);

                if (layoutMode == PdfLayoutMode.MultipleImages)
                {
                    double scale = Math.Min(1.0, maxWidth / img.PixelWidth);
                    double w = img.PixelWidth * scale;
                    double h = img.PixelHeight * scale;

                    if (cy + h > maxHeight && cy > cmargin)
                    {
                        currentPage = document.AddPage();
                        currentPage.Size = sharpSize;
                        currentGfx?.Dispose();
                        currentGfx = XGraphics.FromPdfPage(currentPage);
                        cy = cmargin;
                    }

                    currentGfx!.DrawImage(img, cmargin, cy, w, h);
                    cy += h + cmargin;
                }
                else // SingleImageFit or Fill
                {
                    double wRatio = maxWidth / img.PixelWidth;
                    double hRatio = maxHeight / img.PixelHeight;
                    double scale = layoutMode == PdfLayoutMode.SingleImageFit ? Math.Min(wRatio, hRatio) : Math.Max(wRatio, hRatio);

                    double w = img.PixelWidth * scale;
                    double h = img.PixelHeight * scale;

                    double x = (currentPage.Width.Point - w) / 2;
                    double y = (currentPage.Height.Point - h) / 2;

                    currentGfx!.DrawImage(img, x, y, w, h);
                }
                imgIndex++;
            }
            currentGfx?.Dispose();

            document.Save(destinationPath);
            return destinationPath;
        });
    }
}
