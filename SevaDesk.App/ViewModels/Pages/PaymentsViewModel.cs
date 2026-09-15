using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SevaDesk.Core.Models;
using SevaDesk_App.Services;

namespace SevaDesk_App.ViewModels.Pages;

public partial class PaymentsViewModel : ObservableObject
{
    [ObservableProperty]
    private ObservableCollection<ServiceRateItem> _rateCard = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCartItems))]
    private ObservableCollection<CartItem> _cartItems = [];

    public bool HasCartItems => CartItems.Count > 0;

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

    public PaymentsViewModel()
    {
        InitializeRateCard();
        var savedUpi = AppServices.Database.GetSetting("shop_upi_id");
        if (!string.IsNullOrWhiteSpace(savedUpi))
        {
            ShopUpiId = savedUpi;
        }
        _ = LoadPaymentsDataAsync();
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

    public async Task LoadPaymentsDataAsync()
    {
        RecentTransactions.Clear();
        var payments = await AppServices.Payments.GetRecentPaymentsAsync(50);
        foreach (var p in payments)
        {
            RecentTransactions.Add(new TransactionItem
            {
                Id = p.Id,
                InvoiceNo = p.InvoiceNo,
                CustomerName = p.CustomerName,
                Amount = p.Amount,
                PaymentMode = p.PaymentMethod,
                Status = "Paid",
                Time = p.PaymentDate.ToLocalTime()
            });
        }

        var summary = await AppServices.Payments.GetTodaySalesSummaryAsync();
        TodayTotalSales = summary.TotalSales;
        TodayCashTotal = summary.CashTotal;
        TodayUpiTotal = summary.UpiTotal;
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
    public async Task CompletePaymentAsync(string paymentMode)
    {
        if (CartItems.Count == 0) return;

        var invoiceNo = await AppServices.Payments.GenerateNextInvoiceNoAsync();
        var custName = string.IsNullOrWhiteSpace(CustomerName) ? "Walk-in Customer" : CustomerName.Trim();
        var itemsSummary = string.Join(", ", CartItems.Select(c => $"{c.ServiceName} x{c.Quantity}"));

        var payment = new Payment
        {
            InvoiceNo = invoiceNo,
            CustomerName = custName,
            Amount = GrandTotal,
            PaymentMethod = paymentMode,
            PaymentDate = DateTime.UtcNow,
            ItemsSummary = itemsSummary
        };

        await AppServices.Payments.RecordPaymentAsync(payment);

        var newTx = new TransactionItem
        {
            Id = payment.Id,
            InvoiceNo = invoiceNo,
            CustomerName = custName,
            Amount = payment.Amount,
            PaymentMode = paymentMode,
            Status = "Paid",
            Time = DateTime.Now
        };

        RecentTransactions.Insert(0, newTx);

        var summary = await AppServices.Payments.GetTodaySalesSummaryAsync();
        TodayTotalSales = summary.TotalSales;
        TodayCashTotal = summary.CashTotal;
        TodayUpiTotal = summary.UpiTotal;

        StatusMessage = $"Payment of ₹{GrandTotal} recorded via {paymentMode} ({invoiceNo})!";

        CartItems.Clear();
        UpdateCartTotals();
        CustomerName = "Walk-in Customer";
    }

    private void UpdateCartTotals()
    {
        GrandTotal = CartItems.Sum(c => c.Total);
        OnPropertyChanged(nameof(HasCartItems));
        var encName = Uri.EscapeDataString("SevaDesk Cyber Cafe");
        UpiDeepLink = $"upi://pay?pa={ShopUpiId}&pn={encName}&am={GrandTotal}&cu=INR";
    }

    public void ApplyBillingHandover(BillingHandoverRequest handover)
    {
        if (handover == null) return;

        if (!string.IsNullOrWhiteSpace(handover.CustomerName))
        {
            CustomerName = handover.CustomerName;
        }

        CartItems.Clear();
        foreach (var item in handover.Items)
        {
            CartItems.Add(item);
        }

        UpdateCartTotals();
        StatusMessage = $"Pre-loaded fees from session for {CustomerName}. Total: ₹{GrandTotal:N0}";
    }
}
