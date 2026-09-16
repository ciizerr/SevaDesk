using OpenCvSharp;
using SevaDesk.Core.Interfaces;
using SevaDesk.Core.Models;
using System.IO;

namespace SevaDesk.Infrastructure.FileManager;

public class ImageProcessingService : IImageProcessingService
{
    private Mat ApplyTransforms(string sourceImagePath, ImageTransformModel config)
    {
        var mat = Cv2.ImRead(sourceImagePath, ImreadModes.Unchanged);
        if (mat.Empty()) throw new InvalidOperationException("Failed to load image.");

        if (config.Grayscale && mat.Channels() > 1)
        {
            var gray = new Mat();
            Cv2.CvtColor(mat, gray, ColorConversionCodes.BGR2GRAY);
            mat.Dispose();
            mat = gray;
        }

        if (config.RotationAngle != 0)
        {
            var center = new Point2f(mat.Cols / 2f, mat.Rows / 2f);
            var rotationMatrix = Cv2.GetRotationMatrix2D(center, config.RotationAngle, 1.0);
            var rotated = new Mat();
            Cv2.WarpAffine(mat, rotated, rotationMatrix, new Size(mat.Cols, mat.Rows));
            mat.Dispose();
            mat = rotated;
        }

        if (config.IsCropEnabled)
        {
            var rect = new Rect(config.CropX, config.CropY, config.CropWidth, config.CropHeight);
            var cropped = new Mat(mat, rect);
            var cloned = cropped.Clone();
            mat.Dispose();
            mat = cloned;
        }

        if (config.Brightness != 0 || config.Contrast != 1.0)
        {
            var adjusted = new Mat();
            mat.ConvertTo(adjusted, -1, config.Contrast, config.Brightness);
            mat.Dispose();
            mat = adjusted;
        }

        if (config.Sharpen)
        {
            var blurred = new Mat();
            Cv2.GaussianBlur(mat, blurred, new Size(0, 0), 3);
            var sharpened = new Mat();
            Cv2.AddWeighted(mat, 1.5, blurred, -0.5, 0, sharpened);
            mat.Dispose();
            blurred.Dispose();
            mat = sharpened;
        }

        return mat;
    }

    public Task<string> ApplyTransformationsAsync(string sourceImagePath, string destinationPath, ImageTransformModel config)
    {
        return Task.Run(() =>
        {
            using var mat = ApplyTransforms(sourceImagePath, config);
            Cv2.ImWrite(destinationPath, mat);
            return destinationPath;
        });
    }

    public Task<Stream> GetPreviewStreamAsync(string sourceImagePath, ImageTransformModel config)
    {
        return Task.Run<Stream>(() =>
        {
            using var mat = ApplyTransforms(sourceImagePath, config);
            Cv2.ImEncode(".jpg", mat, out byte[] buf);
            return new MemoryStream(buf);
        });
    }
}
