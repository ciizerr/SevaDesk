using CommunityToolkit.Mvvm.ComponentModel;

namespace SevaDesk.Core.Models;

public partial class ServiceRateItem : ObservableObject
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    [ObservableProperty]
    private string _serviceName = string.Empty;

    [ObservableProperty]
    private decimal _rate;

    public double RateDouble
    {
        get => (double)Rate;
        set => Rate = (decimal)value;
    }

    partial void OnRateChanged(decimal value)
    {
        OnPropertyChanged(nameof(RateDouble));
    }

    [ObservableProperty]
    private string _unit = "page"; // page, form, doc

    [ObservableProperty]
    private string _glyph = "\uE749"; // Print/Card glyph

    [ObservableProperty]
    private string _category = "Printing";

    [ObservableProperty]
    private bool _isActive = true;

    [ObservableProperty]
    private bool _isEditMode;
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
    public string FormattedDate => Time.ToString("dd MMM yyyy, hh:mm tt");
    public string? ItemsSummary { get; set; }
    public string Glyph => PaymentMode == "UPI" ? "\uE8C7" : "\uE717";
}

public class BillingHandoverRequest
{
    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerId { get; set; }
    public string? SessionId { get; set; }
    public List<CartItem> Items { get; set; } = [];
}

public class EarningsChartBar
{
    public string Label { get; set; } = string.Empty;
    public decimal Total { get; set; }
    public decimal Cash { get; set; }
    public decimal Upi { get; set; }
    public string Tooltip { get; set; } = string.Empty;
}
