using SevaDesk.Core.Models;

namespace SevaDesk.Core.Interfaces;

public interface IImageProcessingService
{
    Task<string> ApplyTransformationsAsync(string sourceImagePath, string destinationPath, ImageTransformModel config);
    Task<Stream> GetPreviewStreamAsync(string sourceImagePath, ImageTransformModel config);
}
