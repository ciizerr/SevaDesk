using System;
using System.IO;
using PdfSharpCore;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using SevaDesk.Core.Models;

namespace SevaDesk.Infrastructure.FileManager;

public static class InvoicePdfBuilder
{
    public static string GenerateA4InvoicePdf(CompletedReceiptInfo receipt, string shopName, string shopAddress, string shopContact, string destinationPath)
    {
        var dir = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        using var document = new PdfDocument();
        document.Info.Title = $"Invoice - {receipt.InvoiceNo}";
        document.Info.Author = shopName;
        document.Info.Subject = $"Customer: {receipt.CustomerName}, Total: Rs. {receipt.GrandTotal:N0}";

        var page = document.AddPage();
        page.Size = PageSize.A4; // 595.3 x 841.9 points

        using var gfx = XGraphics.FromPdfPage(page);

        double margin = 40;
        double pageWidth = page.Width.Point;
        double pageHeight = page.Height.Point;
        double contentWidth = pageWidth - (margin * 2);

        // Fonts
        var fontShopTitle = new XFont("Arial", 15, XFontStyle.Bold);
        var fontDocTitle = new XFont("Arial", 16, XFontStyle.Bold);
        var fontHeader = new XFont("Arial", 8.5, XFontStyle.Bold);
        var fontBody = new XFont("Arial", 9.5, XFontStyle.Regular);
        var fontBodyBold = new XFont("Arial", 9.5, XFontStyle.Bold);
        var fontSmall = new XFont("Arial", 8.5, XFontStyle.Regular);
        var fontMono = new XFont("Courier New", 10.5, XFontStyle.Bold);
        var fontTotal = new XFont("Arial", 12.5, XFontStyle.Bold);

        // Colors
        var colorDark = XColor.FromArgb(15, 23, 42);      // Slate-900
        var colorMuted = XColor.FromArgb(71, 85, 105);    // Slate-600
        var colorLight = XColor.FromArgb(148, 163, 184);  // Slate-400
        var colorBorder = XColor.FromArgb(226, 232, 240); // Slate-200
        var colorHeaderBg = XColor.FromArgb(241, 245, 249); // Slate-100
        var colorGreen = XColor.FromArgb(5, 150, 105);    // Emerald-600
        var colorGreenBg = XColor.FromArgb(236, 253, 245);

        var brushDark = new XSolidBrush(colorDark);
        var brushMuted = new XSolidBrush(colorMuted);
        var penBorder = new XPen(colorBorder, 1);

        double y = margin;

        // 1. Header Row
        // Left: Shop Name & Details
        gfx.DrawString(shopName, fontShopTitle, brushDark, new XPoint(margin, y + 14));
        y += 24;

        if (!string.IsNullOrWhiteSpace(shopAddress))
        {
            gfx.DrawString(shopAddress, fontSmall, brushMuted, new XPoint(margin, y));
            y += 13;
        }

        if (!string.IsNullOrWhiteSpace(shopContact))
        {
            gfx.DrawString($"Phone: {shopContact}", fontSmall, brushMuted, new XPoint(margin, y));
            y += 13;
        }

        // Right: "INVOICE" Title & Meta
        double rightColX = pageWidth - margin;
        double rightColTop = margin;

        gfx.DrawString("INVOICE", fontDocTitle, brushDark, new XRect(margin, rightColTop, contentWidth, 20), XStringFormats.TopRight);
        rightColTop += 22;

        gfx.DrawString(receipt.InvoiceNo, fontMono, brushDark, new XRect(margin, rightColTop, contentWidth, 14), XStringFormats.TopRight);
        rightColTop += 15;

        gfx.DrawString($"Date: {receipt.PaymentDate:dd-MMM-yyyy hh:mm tt}", fontSmall, brushMuted, new XRect(margin, rightColTop, contentWidth, 12), XStringFormats.TopRight);
        rightColTop += 16;

        // Status pill: PAID
        string statusText = $"PAID ({receipt.PaymentMode.ToUpperInvariant()})";
        double pillWidth = 80;
        double pillHeight = 18;
        double pillX = rightColX - pillWidth;
        gfx.DrawRoundedRectangle(new XPen(colorGreen, 1), new XSolidBrush(colorGreenBg), pillX, rightColTop, pillWidth, pillHeight, 3, 3);
        gfx.DrawString(statusText, fontHeader, new XSolidBrush(colorGreen), new XRect(pillX, rightColTop, pillWidth, pillHeight), XStringFormats.Center);

        y = Math.Max(y + 8, rightColTop + pillHeight + 14);

        // Divider
        gfx.DrawLine(penBorder, margin, y, pageWidth - margin, y);
        y += 12;

        // 2. Billed To Customer Card
        bool hasAddress = !string.IsNullOrWhiteSpace(receipt.CustomerAddress);
        double custCardHeight = hasAddress ? 58 : 44;

        gfx.DrawRoundedRectangle(penBorder, new XSolidBrush(XColor.FromArgb(248, 250, 252)), margin, y, contentWidth, custCardHeight, 4, 4);

        double custY = y + 12;
        gfx.DrawString("BILLED TO", fontHeader, new XSolidBrush(colorLight), new XPoint(margin + 12, custY));
        custY += 14;

        string custName = string.IsNullOrWhiteSpace(receipt.CustomerName) ? "Walk-in Customer" : receipt.CustomerName;
        gfx.DrawString(custName, fontBodyBold, brushDark, new XPoint(margin + 12, custY));

        if (!string.IsNullOrWhiteSpace(receipt.CustomerMobile))
        {
            gfx.DrawString($"Phone: {receipt.CustomerMobile}", fontSmall, brushMuted, new XRect(margin + 12, custY - 10, contentWidth - 24, 12), XStringFormats.TopRight);
        }

        if (hasAddress)
        {
            custY += 13;
            gfx.DrawString($"Address: {receipt.CustomerAddress!.Trim()}", fontSmall, brushMuted, new XPoint(margin + 12, custY));
        }

        y += custCardHeight + 16;

        // 3. Items Table Header
        double tableTop = y;
        double headerHeight = 22;
        gfx.DrawRectangle(new XSolidBrush(colorHeaderBg), margin, tableTop, contentWidth, headerHeight);

        double colNumX = margin + 10;
        double colDescX = margin + 35;
        double colQtyX = margin + 310;
        double colRateX = margin + 410;
        double colAmtX = margin + contentWidth - 10;

        gfx.DrawString("#", fontHeader, brushMuted, new XPoint(colNumX, tableTop + 14));
        gfx.DrawString("DESCRIPTION", fontHeader, brushMuted, new XPoint(colDescX, tableTop + 14));
        gfx.DrawString("QTY", fontHeader, brushMuted, new XRect(colQtyX - 20, tableTop, 40, headerHeight), XStringFormats.Center);
        gfx.DrawString("RATE", fontHeader, brushMuted, new XRect(colRateX - 50, tableTop, 50, headerHeight), XStringFormats.CenterRight);
        gfx.DrawString("AMOUNT", fontHeader, brushMuted, new XRect(colAmtX - 60, tableTop, 60, headerHeight), XStringFormats.CenterRight);

        y += headerHeight;

        // 4. Table Rows
        var penRowDivider = new XPen(XColor.FromArgb(241, 245, 249), 1);
        double rowHeight = 22;
        int index = 1;

        if (receipt.Items != null && receipt.Items.Count > 0)
        {
            foreach (var item in receipt.Items)
            {
                gfx.DrawString(index.ToString(), fontSmall, brushMuted, new XPoint(colNumX, y + 14));
                gfx.DrawString(item.Name, fontBody, brushDark, new XPoint(colDescX, y + 14));
                gfx.DrawString(item.Quantity.ToString(), fontBody, brushMuted, new XRect(colQtyX - 20, y, 40, rowHeight), XStringFormats.Center);
                gfx.DrawString($"Rs. {item.Rate:N0}", fontBody, brushMuted, new XRect(colRateX - 50, y, 50, rowHeight), XStringFormats.CenterRight);
                gfx.DrawString($"Rs. {item.Total:N0}", fontBodyBold, brushDark, new XRect(colAmtX - 60, y, 60, rowHeight), XStringFormats.CenterRight);

                y += rowHeight;
                gfx.DrawLine(penRowDivider, margin, y, pageWidth - margin, y);
                index++;
            }
        }
        else if (!string.IsNullOrWhiteSpace(receipt.ItemsSummary))
        {
            gfx.DrawString("1", fontSmall, brushMuted, new XPoint(colNumX, y + 14));
            gfx.DrawString(receipt.ItemsSummary, fontBody, brushDark, new XPoint(colDescX, y + 14));
            gfx.DrawString("1", fontBody, brushMuted, new XRect(colQtyX - 20, y, 40, rowHeight), XStringFormats.Center);
            gfx.DrawString($"Rs. {receipt.GrandTotal:N0}", fontBody, brushMuted, new XRect(colRateX - 50, y, 50, rowHeight), XStringFormats.CenterRight);
            gfx.DrawString($"Rs. {receipt.GrandTotal:N0}", fontBodyBold, brushDark, new XRect(colAmtX - 60, y, 60, rowHeight), XStringFormats.CenterRight);
            y += rowHeight;
            gfx.DrawLine(penRowDivider, margin, y, pageWidth - margin, y);
        }

        y += 12;

        // 5. Totals Block (Right Aligned)
        double totalBlockWidth = 200;
        double totalBlockX = pageWidth - margin - totalBlockWidth;

        // Subtotal
        gfx.DrawString("Subtotal:", fontBody, brushMuted, new XPoint(totalBlockX, y + 12));
        gfx.DrawString($"Rs. {receipt.SubTotal:N0}", fontBodyBold, brushDark, new XRect(totalBlockX, y, totalBlockWidth, 14), XStringFormats.TopRight);
        y += 16;

        if (receipt.Discount > 0)
        {
            gfx.DrawString("Less Discount:", fontBody, new XSolidBrush(colorGreen), new XPoint(totalBlockX, y + 12));
            gfx.DrawString($"-Rs. {receipt.Discount:N0}", fontBodyBold, new XSolidBrush(colorGreen), new XRect(totalBlockX, y, totalBlockWidth, 14), XStringFormats.TopRight);
            y += 16;
        }

        gfx.DrawLine(penBorder, totalBlockX, y + 4, pageWidth - margin, y + 4);
        y += 10;

        // Grand Total
        gfx.DrawString("Total Paid:", fontTotal, brushDark, new XPoint(totalBlockX, y + 14));
        gfx.DrawString($"Rs. {receipt.GrandTotal:N0}", fontTotal, brushDark, new XRect(totalBlockX, y, totalBlockWidth, 16), XStringFormats.TopRight);
        y += 20;

        gfx.DrawString($"Payment Mode: {receipt.PaymentMode}", fontSmall, brushMuted, new XRect(totalBlockX, y, totalBlockWidth, 12), XStringFormats.TopRight);

        // 6. Footer (Pinned to bottom of A4 page)
        double footerY = pageHeight - 50;
        gfx.DrawLine(penBorder, margin, footerY, pageWidth - margin, footerY);
        gfx.DrawString("Thank you for your visit.", fontBody, brushMuted, new XRect(margin, footerY + 12, contentWidth, 14), XStringFormats.Center);

        document.Save(destinationPath);
        return destinationPath;
    }
}
