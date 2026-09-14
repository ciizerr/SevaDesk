using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SevaDesk.Core.Models;

namespace SevaDesk_App.ViewModels;

public partial class PaymentsViewModel : ObservableObject
{
    [ObservableProperty]
    private ObservableCollection<ServiceRateItem> _rateCard = [];

    [ObservableProperty]
    private ObservableCollection<CartItem> _cartItems = [];

    [ObservableProperty]
    private ObservableCollection<TransactionItem> _recentTransactions = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FormattedGrandTotal))]
    private decimal _grandTotal;

    public string FormattedGrandTotal => $"₹{GrandTotal:N0}";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FormattedTodayTotalSales))]
    private decimal _todayTotalSales;

    public string FormattedTodayTotalSales => $"₹{TodayTotalSales:N0}";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FormattedTodayCashTotal))]
    private decimal _todayCashTotal;

    public string FormattedTodayCashTotal => $"₹{TodayCashTotal:N0}";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FormattedTodayUpiTotal))]
    private decimal _todayUpiTotal;

    public string FormattedTodayUpiTotal => $"₹{TodayUpiTotal:N0}";

    [ObservableProperty]
    private string _shopUpiId = "sevadesk.csc@upi";

    [ObservableProperty]
    private string _customerName = "Walk-in Customer";

    [ObservableProperty]
    private string _upiDeepLink = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStatusMessage))]
    private string _statusMessage = string.Empty;

    public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);

    private int _invoiceCounter = 1042;

    public PaymentsViewModel()
    {
        InitializeRateCard();
        LoadSampleTransactions();
        UpdateCartTotals();
    }

    private void InitializeRateCard()
    {
        RateCard.Clear();
        RateCard.Add(new ServiceRateItem { ServiceName = "B&W Print (Single)", Rate = 5, Unit = "page", Category = "Printing", Glyph = "\uE749" });
        RateCard.Add(new ServiceRateItem { ServiceName = "B&W Print (Both Sides)", Rate = 10, Unit = "page", Category = "Printing", Glyph = "\uE749" });
        RateCard.Add(new ServiceRateItem { ServiceName = "Color Print", Rate = 20, Unit = "page", Category = "Printing", Glyph = "\uE790" });
        RateCard.Add(new ServiceRateItem { ServiceName = "Document Scan to PDF", Rate = 15, Unit = "doc", Category = "Scanning", Glyph = "\uE8A5" });
        RateCard.Add(new ServiceRateItem { ServiceName = "A4 Lamination", Rate = 30, Unit = "sheet", Category = "Finishing", Glyph = "\uE7C3" });
        RateCard.Add(new ServiceRateItem { ServiceName = "PVC Card (Aadhaar/PAN)", Rate = 70, Unit = "card", Category = "Cards", Glyph = "\uE8C7" });
        RateCard.Add(new ServiceRateItem { ServiceName = "Govt Online Form Fill", Rate = 100, Unit = "application", Category = "Services", Glyph = "\uE77B" });
        RateCard.Add(new ServiceRateItem { ServiceName = "Urgent Typing / Affidavit", Rate = 80, Unit = "page", Category = "Services", Glyph = "\uE8C1" });
    }

    private void LoadSampleTransactions()
    {
        RecentTransactions.Clear();
        RecentTransactions.Add(new TransactionItem
        {
            InvoiceNo = "INV-1040",
            CustomerName = "Ravi Kumar",
            Amount = 150,
            PaymentMode = "UPI",
            Status = "Paid",
            Time = DateTime.Now.AddMinutes(-40)
        });
        RecentTransactions.Add(new TransactionItem
        {
            InvoiceNo = "INV-1041",
            CustomerName = "Sita Devi",
            Amount = 65,
            PaymentMode = "Cash",
            Status = "Paid",
            Time = DateTime.Now.AddMinutes(-18)
        });

        RecalculateDayTotals();
    }

    private void RecalculateDayTotals()
    {
        TodayTotalSales = RecentTransactions.Sum(t => t.Amount);
        TodayCashTotal = RecentTransactions.Where(t => t.PaymentMode == "Cash").Sum(t => t.Amount);
        TodayUpiTotal = RecentTransactions.Where(t => t.PaymentMode == "UPI").Sum(t => t.Amount);
    }

    [RelayCommand]
    public void AddToCart(ServiceRateItem service)
    {
        var existing = CartItems.FirstOrDefault(c => c.ServiceName == service.ServiceName);
        if (existing != null)
        {
            existing.Quantity += 1;
            // Force collection refresh
            var idx = CartItems.IndexOf(existing);
            CartItems[idx] = existing;
        }
        else
        {
            CartItems.Add(new CartItem
            {
                ServiceName = service.ServiceName,
                Rate = service.Rate,
                Quantity = 1
            });
        }
        UpdateCartTotals();
    }

    [RelayCommand]
    public void IncreaseQty(CartItem item)
    {
        item.Quantity += 1;
        var idx = CartItems.IndexOf(item);
        CartItems[idx] = item;
        UpdateCartTotals();
    }

    [RelayCommand]
    public void DecreaseQty(CartItem item)
    {
        if (item.Quantity > 1)
        {
            item.Quantity -= 1;
            var idx = CartItems.IndexOf(item);
            CartItems[idx] = item;
        }
        else
        {
            CartItems.Remove(item);
        }
        UpdateCartTotals();
    }

    [RelayCommand]
    public void RemoveCartItem(CartItem item)
    {
        CartItems.Remove(item);
        UpdateCartTotals();
    }

    [RelayCommand]
    public void ClearBill()
    {
        CartItems.Clear();
        UpdateCartTotals();
        StatusMessage = "Current bill cleared.";
    }

    [RelayCommand]
    public void CompletePayment(string paymentMode)
    {
        if (CartItems.Count == 0) return;

        _invoiceCounter++;
        var invoiceNo = $"INV-{_invoiceCounter}";
        var newTx = new TransactionItem
        {
            InvoiceNo = invoiceNo,
            CustomerName = string.IsNullOrWhiteSpace(CustomerName) ? "Walk-in Customer" : CustomerName,
            Amount = GrandTotal,
            PaymentMode = paymentMode,
            Status = "Paid",
            Time = DateTime.Now
        };

        RecentTransactions.Insert(0, newTx);
        RecalculateDayTotals();
        StatusMessage = $"Payment of ₹{GrandTotal} recorded via {paymentMode} ({invoiceNo})!";

        CartItems.Clear();
        UpdateCartTotals();
        CustomerName = "Walk-in Customer";
    }

    private void UpdateCartTotals()
    {
        GrandTotal = CartItems.Sum(c => c.Total);
        var encName = Uri.EscapeDataString("SevaDesk Cyber Cafe");
        UpiDeepLink = $"upi://pay?pa={ShopUpiId}&pn={encName}&am={GrandTotal}&cu=INR";
    }
}
