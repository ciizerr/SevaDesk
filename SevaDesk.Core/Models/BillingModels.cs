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

    public string FormattedRate => $"₹{Rate:N0}/{Unit}";

    partial void OnRateChanged(decimal value)
    {
        OnPropertyChanged(nameof(RateDouble));
        OnPropertyChanged(nameof(FormattedRate));
    }

    [ObservableProperty]
    private string _unit = "page"; // page, form, doc

    partial void OnUnitChanged(string value)
    {
        OnPropertyChanged(nameof(FormattedRate));
    }

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
    public decimal OriginalRate { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Total))]
    [NotifyPropertyChangedFor(nameof(FormattedTotal))]
    [NotifyPropertyChangedFor(nameof(FormattedRate))]
    [NotifyPropertyChangedFor(nameof(FormattedOriginalRate))]
    [NotifyPropertyChangedFor(nameof(IsRateModified))]
    private decimal _rate;

    partial void OnRateChanged(decimal value)
    {
        if (OriginalRate == 0 && value > 0)
        {
            OriginalRate = value;
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Total))]
    [NotifyPropertyChangedFor(nameof(FormattedTotal))]
    private int _quantity = 1;

    public decimal Total => Rate * Quantity;
    public string FormattedTotal => $"₹{Total:N0}";
    public string FormattedRate => $"₹{Rate:N0}";
    public string FormattedOriginalRate => $"₹{OriginalRate:N0}";
    public bool IsRateModified => OriginalRate > 0 && Rate != OriginalRate;

    public void ResetToOriginalRate()
    {
        if (OriginalRate > 0)
        {
            Rate = OriginalRate;
        }
    }
}

public partial class TransactionItem : ObservableObject
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string InvoiceNo { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerId { get; set; }
    public decimal SubTotal { get; set; }
    public decimal Discount { get; set; }
    public decimal Amount { get; set; }
    public bool HasDiscount => Discount > 0;
    public string FormattedAmount => $"₹{Amount:N0}";
    public string FormattedSubTotal => $"₹{SubTotal:N0}";
    public string FormattedDiscount => $"-₹{Discount:N0}";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Glyph))]
    private string _paymentMode = "UPI"; // UPI, Cash

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPending))]
    [NotifyPropertyChangedFor(nameof(Glyph))]
    private string _status = "Paid"; // Paid, Pending

    public bool IsPending => string.Equals(Status, "Pending", StringComparison.OrdinalIgnoreCase);
    public DateTime Time { get; set; } = DateTime.Now;
    public string FormattedTime => Time.ToString("hh:mm tt");
    public string FormattedDate => Time.ToString("dd MMM yyyy, hh:mm tt");
    public bool IsDateToday => Time.Date == DateTime.Today;
    public string? ItemsSummary { get; set; }
    public string Glyph => IsPending ? "\uE896" : (PaymentMode == "UPI" ? "\uE8C7" : "\uE717");
}

public class BillingHandoverRequest
{
    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerId { get; set; }
    public string? SessionId { get; set; }
    public string? CustomerAddress { get; set; }
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

public class CompletedReceiptInfo
{
    public string InvoiceNo { get; set; } = string.Empty;
    public string CustomerName { get; set; } = "Walk-in Customer";
    public string CustomerMobile { get; set; } = string.Empty;
    public string? CustomerAddress { get; set; }
    public DateTime PaymentDate { get; set; } = DateTime.Now;
    public decimal SubTotal { get; set; }
    public decimal Discount { get; set; }
    public decimal GrandTotal { get; set; }
    public string PaymentMode { get; set; } = "Cash";
    public string Status { get; set; } = "Paid"; // Paid, Pending
    public bool IsPending => string.Equals(Status, "Pending", StringComparison.OrdinalIgnoreCase);
    public string? UpiPayload { get; set; }
    public string ItemsSummary { get; set; } = string.Empty;
    public List<ReceiptItemInfo> Items { get; set; } = [];

    // Optional Session Context
    public string? SessionDurationText { get; set; }
    public string? SessionNotes { get; set; }
    public bool HasSessionInfo => !string.IsNullOrWhiteSpace(SessionDurationText) || !string.IsNullOrWhiteSpace(SessionNotes);

    public static CompletedReceiptInfo FromPayment(Payment payment, Customer? customer = null, Session? session = null)
    {
        var subTotal = payment.SubTotal > 0 ? payment.SubTotal : (payment.Amount + payment.Discount);
        var receipt = new CompletedReceiptInfo
        {
            InvoiceNo = string.IsNullOrWhiteSpace(payment.InvoiceNo) ? "RCP-DIRECT" : payment.InvoiceNo,
            CustomerName = !string.IsNullOrWhiteSpace(customer?.Name) ? customer.Name : (string.IsNullOrWhiteSpace(payment.CustomerName) ? "Walk-in Customer" : payment.CustomerName),
            CustomerMobile = customer?.Mobile ?? string.Empty,
            CustomerAddress = customer?.Village,
            PaymentDate = payment.PaymentDate.ToLocalTime(),
            GrandTotal = payment.Amount,
            SubTotal = subTotal,
            Discount = payment.Discount,
            PaymentMode = string.IsNullOrWhiteSpace(payment.PaymentMethod) ? "Cash" : payment.PaymentMethod,
            Status = string.IsNullOrWhiteSpace(payment.Status) ? "Paid" : payment.Status,
            ItemsSummary = payment.ItemsSummary ?? string.Empty
        };

        if (session != null)
        {
            var durationSec = session.DurationSeconds;
            int mins = durationSec / 60;
            int secs = durationSec % 60;
            receipt.SessionDurationText = mins > 0 ? $"{mins}m {secs}s" : $"{secs}s";
            receipt.SessionNotes = session.Notes;
        }

        if (payment.BilledItems != null && payment.BilledItems.Count > 0)
        {
            foreach (var b in payment.BilledItems)
            {
                var qty = b.Quantity > 0 ? b.Quantity : 1;
                var tot = b.LineTotal > 0 ? b.LineTotal : payment.Amount;
                receipt.Items.Add(new ReceiptItemInfo
                {
                    Name = b.ServiceName,
                    Quantity = qty,
                    Rate = tot / Math.Max(1, qty),
                    Total = tot
                });
            }
        }
        else
        {
            receipt.Items.Add(new ReceiptItemInfo
            {
                Name = !string.IsNullOrWhiteSpace(payment.Notes) ? payment.Notes : "Counter Service",
                Quantity = 1,
                Rate = payment.Amount,
                Total = payment.Amount
            });
        }

        return receipt;
    }
}

public class ReceiptItemInfo
{
    public string Name { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
    public decimal Rate { get; set; }
    public decimal Total { get; set; }
}

