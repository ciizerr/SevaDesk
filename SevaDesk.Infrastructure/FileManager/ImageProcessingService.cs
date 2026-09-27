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
            var ext = Path.GetExtension(destinationPath).ToLower();
            if (ext == ".jpg" || ext == ".jpeg")
            {
                Cv2.ImWrite(destinationPath, mat, new ImageEncodingParam(ImwriteFlags.JpegQuality, config.Quality));
            }
            else
            {
                Cv2.ImWrite(destinationPath, mat);
            }
            return destinationPath;
        });
    }

    public Task<Stream> GetPreviewStreamAsync(string sourceImagePath, ImageTransformModel config)
    {
        return Task.Run<Stream>(() =>
        {
            using var mat = ApplyTransforms(sourceImagePath, config);
            Cv2.ImEncode(".jpg", mat, out byte[] buf, new ImageEncodingParam(ImwriteFlags.JpegQuality, config.Quality));
            return new MemoryStream(buf);
        });
    }

    public Task<string> CompressToTargetSizeAsync(string sourceImagePath, string destinationPath, int minKb, int maxKb, double? targetAspectRatio = null, bool enhanceContrast = false)
    {
        return Task.Run(() =>
        {
            if (!File.Exists(sourceImagePath))
                throw new FileNotFoundException("Source image not found.", sourceImagePath);

            var mat = Cv2.ImRead(sourceImagePath, ImreadModes.Color);
            if (mat.Empty()) throw new InvalidOperationException("Failed to read image with OpenCV.");

            try
            {
                // 1. Contrast enhancement (especially helpful for clean white background on signatures)
                if (enhanceContrast)
                {
                    if (mat.Channels() > 1)
                    {
                        var gray = new Mat();
                        Cv2.CvtColor(mat, gray, ColorConversionCodes.BGR2GRAY);
                        mat.Dispose();
                        mat = gray;
                    }

                    var enhanced = new Mat();
                    mat.ConvertTo(enhanced, -1, 1.35, -20);
                    mat.Dispose();
                    mat = enhanced;
                }

                // 2. Aspect ratio adjustment (center crop if needed)
                if (targetAspectRatio.HasValue && targetAspectRatio.Value > 0)
                {
                    double currentAspect = (double)mat.Cols / mat.Rows;
                    if (Math.Abs(currentAspect - targetAspectRatio.Value) > 0.05)
                    {
                        int newW = mat.Cols;
                        int newH = mat.Rows;
                        if (currentAspect > targetAspectRatio.Value)
                        {
                            newW = (int)(mat.Rows * targetAspectRatio.Value);
                        }
                        else
                        {
                            newH = (int)(mat.Cols / targetAspectRatio.Value);
                        }

                        int startX = Math.Max(0, (mat.Cols - newW) / 2);
                        int startY = Math.Max(0, (mat.Rows - newH) / 2);
                        var cropRect = new Rect(startX, startY, Math.Min(newW, mat.Cols - startX), Math.Min(newH, mat.Rows - startY));
                        var cropped = new Mat(mat, cropRect);
                        var cloned = cropped.Clone();
                        mat.Dispose();
                        cropped.Dispose();
                        mat = cloned;
                    }
                }

                // 3. Scale down excessive resolution based on target file size
                int maxDimension = maxKb switch
                {
                    <= 20 => 500,
                    <= 50 => 650,
                    <= 100 => 950,
                    <= 200 => 1400,
                    _ => 1800
                };

                if (mat.Cols > maxDimension || mat.Rows > maxDimension)
                {
                    double scale = (double)maxDimension / Math.Max(mat.Cols, mat.Rows);
                    var resized = new Mat();
                    Cv2.Resize(mat, resized, new Size(mat.Cols * scale, mat.Rows * scale), 0, 0, InterpolationFlags.Area);
                    mat.Dispose();
                    mat = resized;
                }

                // 4. Iterative JPEG compression
                byte[] bestBuf = [];
                for (int q = 92; q >= 15; q -= 5)
                {
                    Cv2.ImEncode(".jpg", mat, out byte[] currentBuf, new ImageEncodingParam(ImwriteFlags.JpegQuality, q));
                    int sizeKb = currentBuf.Length / 1024;

                    bestBuf = currentBuf;
                    if (sizeKb <= maxKb)
                    {
                        // Within target upper bound!
                        break;
                    }
                }

                // 5. If still exceeding maxKb even at quality 15, scale down iteratively
                if (bestBuf.Length / 1024 > maxKb)
                {
                    for (int attempt = 0; attempt < 3 && bestBuf.Length / 1024 > maxKb; attempt++)
                    {
                        var smaller = new Mat();
                        Cv2.Resize(mat, smaller, new Size(mat.Cols * 0.75, mat.Rows * 0.75), 0, 0, InterpolationFlags.Area);
                        mat.Dispose();
                        mat = smaller;
                        Cv2.ImEncode(".jpg", mat, out bestBuf, new ImageEncodingParam(ImwriteFlags.JpegQuality, 35));
                    }
                }

                var destDir = Path.GetDirectoryName(destinationPath);
                if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
                {
                    Directory.CreateDirectory(destDir);
                }

                File.WriteAllBytes(destinationPath, bestBuf);
                return destinationPath;
            }
            finally
            {
                mat?.Dispose();
            }
        });
    }
}

