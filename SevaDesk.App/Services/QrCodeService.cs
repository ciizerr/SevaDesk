using System;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Media.Imaging;
using QRCoder;

namespace SevaDesk_App.Services;

public class QrCodeService
{
    public string BuildUpiPayload(string? vpa, string? payeeName, decimal? amount = null, string? transactionNote = null)
    {
        var cleanVpa = (vpa ?? "").Trim();
        var cleanName = Uri.EscapeDataString(string.IsNullOrWhiteSpace(payeeName) ? "My Shop" : payeeName.Trim());

        var payload = $"upi://pay?pa={cleanVpa}&pn={cleanName}&cu=INR";
        if (amount.HasValue && amount.Value > 0)
        {
            payload += $"&am={amount.Value:F2}";
        }
        if (!string.IsNullOrWhiteSpace(transactionNote))
        {
            payload += $"&tn={Uri.EscapeDataString(transactionNote.Trim())}";
        }
        return payload;
    }

    public byte[] GenerateQrPngBytes(string content, int pixelsPerModule = 10)
    {
        if (string.IsNullOrWhiteSpace(content)) return Array.Empty<byte>();

        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(content, QRCodeGenerator.ECCLevel.M);
        var qr = new PngByteQRCode(data);
        return qr.GetGraphic(pixelsPerModule);
    }

    /// <summary>
    /// Generates a crisp, pixel-perfect WriteableBitmap directly in memory without async stream deadlocks.
    /// Runs synchronously in less than 1ms on the UI thread.
    /// </summary>
    public WriteableBitmap? GenerateQrBitmap(string content, int pixelsPerModule = 8)
    {
        if (string.IsNullOrWhiteSpace(content)) return null;

        try
        {
            using var generator = new QRCodeGenerator();
            using var data = generator.CreateQrCode(content, QRCodeGenerator.ECCLevel.M);
            var matrix = data.ModuleMatrix;
            int moduleCount = matrix.Count;
            const int quietZone = 2; // Clean border quiet-zone
            int totalModules = moduleCount + (quietZone * 2);
            int width = totalModules * pixelsPerModule;
            int height = width;

            var wb = new WriteableBitmap(width, height);
            using (var stream = wb.PixelBuffer.AsStream())
            {
                byte[] bgra = new byte[width * height * 4];

                // Default entire canvas to solid white (A=255, R=255, G=255, B=255)
                Array.Fill(bgra, (byte)255);

                for (int r = 0; r < moduleCount; r++)
                {
                    var row = matrix[r];
                    int startY = (r + quietZone) * pixelsPerModule;

                    for (int c = 0; c < moduleCount; c++)
                    {
                        if (row[c]) // Dark module
                        {
                            int startX = (c + quietZone) * pixelsPerModule;

                            for (int py = 0; py < pixelsPerModule; py++)
                            {
                                int y = startY + py;
                                int rowOffset = y * width * 4;

                                for (int px = 0; px < pixelsPerModule; px++)
                                {
                                    int x = startX + px;
                                    int idx = rowOffset + x * 4;
                                    bgra[idx] = 0;       // Blue
                                    bgra[idx + 1] = 0;   // Green
                                    bgra[idx + 2] = 0;   // Red
                                    bgra[idx + 3] = 255; // Alpha
                                }
                            }
                        }
                    }
                }

                stream.Write(bgra, 0, bgra.Length);
            }
            wb.Invalidate();
            return wb;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[QrCodeService] Failed to generate QR: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Async compatibility wrapper returning the synchronous WriteableBitmap.
    /// </summary>
    public Task<WriteableBitmap?> GenerateQrBitmapAsync(string content, int pixelsPerModule = 8)
    {
        return Task.FromResult(GenerateQrBitmap(content, pixelsPerModule));
    }
}
