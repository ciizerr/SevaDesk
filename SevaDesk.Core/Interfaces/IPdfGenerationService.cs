namespace SevaDesk.Core.Interfaces;

public enum PdfPaperSize { A4, A3, Letter, Legal }
public enum PdfLayoutMode { SingleImageFit, SingleImageFill, MultipleImages }

public interface IPdfGenerationService
{
    Task<string> GeneratePdfAsync(IReadOnlyList<string> imagePaths, string destinationPath, PdfPaperSize paperSize, PdfLayoutMode layoutMode);
}
