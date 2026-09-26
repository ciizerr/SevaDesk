using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace SevaDesk.Core.Models;

public class BilledServiceItem
{
    public string ServiceName { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
    public decimal UnitRate { get; set; }
    public decimal LineTotal { get; set; }
    public bool HasRate => UnitRate > 0;
    public bool IsDiscount { get; set; }
    public string FormattedRate => UnitRate > 0 ? $"@ ₹{UnitRate:N0}" : string.Empty;
    public string FormattedTotal => IsDiscount
        ? (LineTotal > 0 ? $"-₹{LineTotal:N0}" : "-₹0")
        : (LineTotal > 0 ? $"₹{LineTotal:N0}" : string.Empty);
    public string FormattedQty => IsDiscount ? "DISCOUNT" : (Quantity > 1 ? $"×{Quantity}" : "×1");
    public string DisplayChipText
    {
        get
        {
            if (IsDiscount) return $"Disc: {FormattedTotal}";
            return Quantity > 1 ? $"{ServiceName} ×{Quantity}" : ServiceName;
        }
    }
    public string Glyph { get; set; } = "\uE749";

    public static List<BilledServiceItem> ParseSummary(string? summary)
    {
        var items = new List<BilledServiceItem>();
        if (string.IsNullOrWhiteSpace(summary))
        {
            return items;
        }

        var parts = summary.Split(new[] { ", " }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var rawPart in parts)
        {
            var part = rawPart.Trim();
            if (string.IsNullOrWhiteSpace(part)) continue;

            // 1. Discount item: [Disc: -₹10] or [Disc: 10%] or Disc: -₹10
            if (part.StartsWith("[Disc:", StringComparison.OrdinalIgnoreCase) || part.StartsWith("Disc:", StringComparison.OrdinalIgnoreCase))
            {
                var cleanDisc = part.Trim('[', ']').Replace("Disc:", "", StringComparison.OrdinalIgnoreCase).Trim();
                var numericPart = cleanDisc.TrimStart('-', '₹').Trim();
                decimal.TryParse(numericPart, NumberStyles.Any, CultureInfo.InvariantCulture, out var discAmount);

                items.Add(new BilledServiceItem
                {
                    ServiceName = "Discount Applied",
                    Quantity = 1,
                    IsDiscount = true,
                    LineTotal = discAmount,
                    Glyph = "\uE8C0" // Discount / Tag glyph
                });
                continue;
            }

            // 2. Standard Service item: "{ServiceName} x{Quantity} @ ₹{Rate}" (with optional "(orig ₹{OriginalRate})")
            var match = Regex.Match(part, @"^(?<name>.+?)\s+x(?<qty>\d+)\s+@\s+₹(?<rate>[\d\.,]+)(?:\s*\(orig\s*₹[\d\.,]+\))?", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                var name = match.Groups["name"].Value.Trim();
                int.TryParse(match.Groups["qty"].Value, out var qty);
                if (qty <= 0) qty = 1;

                var rateStr = match.Groups["rate"].Value.Replace(",", "");
                decimal.TryParse(rateStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var rate);

                var total = qty * rate;

                items.Add(new BilledServiceItem
                {
                    ServiceName = name,
                    Quantity = qty,
                    UnitRate = rate,
                    LineTotal = total,
                    Glyph = ResolveGlyph(name)
                });
            }
            else
            {
                // 3. Fallback for non-rate items (e.g. "General Desk Session", "Application Form", or session notes)
                items.Add(new BilledServiceItem
                {
                    ServiceName = part,
                    Quantity = 1,
                    LineTotal = 0,
                    Glyph = ResolveGlyph(part)
                });
            }
        }

        return items;
    }

    private static string ResolveGlyph(string serviceName)
    {
        var lower = serviceName.ToLowerInvariant();
        if (lower.Contains("print") || lower.Contains("xerox") || lower.Contains("copy")) return "\uE74E"; // Print
        if (lower.Contains("card") || lower.Contains("aadhaar") || lower.Contains("pan") || lower.Contains("id")) return "\uE8F1"; // ID Card
        if (lower.Contains("photo") || lower.Contains("passport") || lower.Contains("camera")) return "\uE7C5"; // Camera/Photo
        if (lower.Contains("cert") || lower.Contains("stamp") || lower.Contains("affidavit")) return "\uE91B"; // Certificate
        if (lower.Contains("fee") || lower.Contains("bill") || lower.Contains("pay")) return "\uE9D9"; // Bill / Cash
        if (lower.Contains("scan") || lower.Contains("doc")) return "\uE749"; // Document
        if (lower.Contains("online") || lower.Contains("portal") || lower.Contains("form") || lower.Contains("apply")) return "\uE8A5"; // Form / Checklist
        if (lower.Contains("desk") || lower.Contains("workstation") || lower.Contains("browsing")) return "\uE7BE"; // Desktop / Desk
        return "\uE749"; // Default Document glyph
    }
}
