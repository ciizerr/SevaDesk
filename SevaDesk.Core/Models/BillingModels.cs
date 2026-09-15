using CommunityToolkit.Mvvm.ComponentModel;

namespace SevaDesk.Core.Models;

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

public class BillingHandoverRequest
{
    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerId { get; set; }
    public string? SessionId { get; set; }
    public List<CartItem> Items { get; set; } = [];
}
