using CommunityToolkit.Mvvm.ComponentModel;

namespace SevaDesk.Core.Models;

public class DocumentItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string Category { get; set; } = "Unorganised"; // Unorganised, Identity, Education, PrintReady
    public string FileSize { get; set; } = "240 KB";
    public string Extension { get; set; } = ".pdf";
    public string Status { get; set; } = "Pending Review";
    public string Glyph { get; set; } = "\uE8A5"; // Document glyph
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

public class ApplicationItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Title { get; set; } = string.Empty; // e.g. SSC CGL 2026, PAN Application
    public string CustomerName { get; set; } = string.Empty;
    public string PortalName { get; set; } = string.Empty; // e.g. ssc.gov.in, NSDL
    public string ApplicationNumber { get; set; } = string.Empty;
    public string Status { get; set; } = "Draft"; // Draft, Submitted, Doc Pending, Completed
    public decimal ServiceCharge { get; set; }
    public decimal GovtFee { get; set; }
    public decimal TotalAmount => ServiceCharge + GovtFee;
    public string FormattedTotal => $"₹{TotalAmount:N0}";
    public string FormattedServiceCharge => $"₹{ServiceCharge:N0}";
    public string FormattedGovtFee => $"₹{GovtFee:N0}";
    public string RequiredDocs { get; set; } = "Photo, Signature, Aadhaar, Marksheet";
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

public partial class ResourceItem : ObservableObject
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Title { get; set; } = string.Empty;
    public string Category { get; set; } = "Forms"; // Forms, Affidavits, Notices, Rates
    public string FileType { get; set; } = "PDF";
    public string FileSize { get; set; } = "1.2 MB";
    public string Glyph { get; set; } = "\uE8A5";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StarForeground))]
    private bool _isFavorite;

    public string StarForeground => IsFavorite ? "#F59E0B" : "#808080";
    public DateTime LastModified { get; set; } = DateTime.Now;
}

public class ServiceRateItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string ServiceName { get; set; } = string.Empty;
    public decimal Rate { get; set; }
    public string Unit { get; set; } = "page"; // page, form, doc
    public string Glyph { get; set; } = "\uE749"; // Print/Card glyph
    public string Category { get; set; } = "Printing";
}

public partial class CartItem : ObservableObject
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string ServiceName { get; set; } = string.Empty;
    public decimal Rate { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Total))]
    [NotifyPropertyChangedFor(nameof(FormattedTotal))]
    private int _quantity = 1;

    public decimal Total => Rate * Quantity;
    public string FormattedTotal => $"₹{Total:N0}";
}

public class TransactionItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string InvoiceNo { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string FormattedAmount => $"₹{Amount:N0}";
    public string PaymentMode { get; set; } = "UPI"; // UPI, Cash
    public string Status { get; set; } = "Paid";
    public DateTime Time { get; set; } = DateTime.Now;
    public string FormattedTime => Time.ToString("hh:mm tt");
    public string Glyph => PaymentMode == "UPI" ? "\uE8C7" : "\uE717";
}

public class CompressionPreset
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string TargetSize { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string IconGlyph { get; set; } = "\uEB9F";
}

public class ApplicationChecklistItem
{
    public string Title { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }
    public string Category { get; set; } = "Document";
}

