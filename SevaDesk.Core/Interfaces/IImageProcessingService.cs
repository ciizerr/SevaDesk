using SevaDesk.Core.Models;

namespace SevaDesk.Core.Interfaces;

public interface IImageProcessingService
{
    Task<string> ApplyTransformationsAsync(string sourceImagePath, string destinationPath, ImageTransformModel config);
    Task<Stream> GetPreviewStreamAsync(string sourceImagePath, ImageTransformModel config);
    Task<string> CompressToTargetSizeAsync(string sourceImagePath, string destinationPath, int minKb, int maxKb, double? targetAspectRatio = null, bool enhanceContrast = false);
}

