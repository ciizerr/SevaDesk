namespace SevaDesk.Core.Models;

public class ImageTransformModel
{
    public double Brightness { get; set; } = 0; // -255 to +255
    public double Contrast { get; set; } = 1.0; // 0.0 to 3.0+
    public bool Grayscale { get; set; } = false;
    public double RotationAngle { get; set; } = 0; // Degrees
    public bool Sharpen { get; set; } = false;
    
    // Optional: Crop rectangle
    public int CropX { get; set; }
    public int CropY { get; set; }
    public int CropWidth { get; set; }
    public int CropHeight { get; set; }
    public bool IsCropEnabled => CropWidth > 0 && CropHeight > 0;
    public int Quality { get; set; } = 100; // 1 to 100
}
