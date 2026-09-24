using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SevaDesk.Core.Models;
using SevaDesk_App.Services;

namespace SevaDesk_App.ViewModels.Pages;

public partial class PaymentsViewModel : StatusViewModel
{
    // --- View Switching (POS vs Earnings Report) ---
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPosView))]
    [NotifyPropertyChangedFor(nameof(IsEarningsView))]
    private int _selectedViewIndex = 0;

    public bool IsPosView => SelectedViewIndex == 0;
    public bool IsEarningsView => SelectedViewIndex == 1;

    partial void OnSelectedViewIndexChanged(int value)
    {
        if (value == 1)
        {
            _ = LoadEarningsAsync();
        }
    }

    // --- POS: Collapsible Expander States ---
    [ObservableProperty]
    private bool _isTodayLedgerExpanded = false;

    [ObservableProperty]
    private bool _isScanAndPayExpanded = true;

    public int TodayTransactionCount => RecentTransactions.Count;
    public string TodayLedgerHeaderSummary => $"{RecentTransactions.Count} payments recorded today (₹{TodayTotalSales:N0})";

    // --- POS: Rate Card & Cart ---
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
    [NotifyPropertyChangedFor(nameof(TodayLedgerHeaderSummary))]
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
    private string _payeeName = "SevaDesk Cyber Cafe";

    [ObservableProperty]
    private string _customerName = "Walk-in Customer";

    [ObservableProperty]
    private string _upiDeepLink = string.Empty;

    [ObservableProperty]
    private bool _isEditMode;

    partial void OnIsEditModeChanged(bool value)
    {
        foreach (var item in RateCard)
        {
            item.IsEditMode = value;
        }
    }

    // --- Earnings Report: Filters & State ---
    public ObservableCollection<string> QuickFilters { get; } =
    [
        "Today",
        "This Week",
        "This Month",
        "Last Month",
        "By Year",
        "Custom Range"
    ];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsYearFilterVisible))]
    [NotifyPropertyChangedFor(nameof(IsCustomRangeVisible))]
    private string _selectedQuickFilter = "This Month";

    public bool IsYearFilterVisible => SelectedQuickFilter == "By Year";
    public bool IsCustomRangeVisible => SelectedQuickFilter == "Custom Range";

    public ObservableCollection<int> AvailableYears { get; } = [];

    [ObservableProperty]
    private int _selectedYear = DateTime.Today.Year;

    public ObservableCollection<string> MonthOptions { get; } =
    [
        "All Months",
        "January",
        "February",
        "March",
        "April",
        "May",
        "June",
        "July",
        "August",
        "September",
        "October",
        "November",
        "December"
    ];

    [ObservableProperty]
    private int _selectedMonthIndex = 0; // 0 = All Months, 1 = Jan, ... 12 = Dec

    public ObservableCollection<string> PaymentModeFilters { get; } =
    [
        "All",
        "Cash",
        "UPI"
    ];

    [ObservableProperty]
    private string _selectedPaymentMode = "All";

    [ObservableProperty]
    private DateTimeOffset? _customFromDate = new DateTimeOffset(new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1));

    [ObservableProperty]
    private DateTimeOffset? _customToDate = new DateTimeOffset(DateTime.Today);

    [ObservableProperty]
    private string _searchFilter = string.Empty;

    // --- Earnings Report: Summary & Data ---
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FormattedFilteredTotal))]
    private decimal _filteredTotalSales;
    public string FormattedFilteredTotal => $"₹{FilteredTotalSales:N0}";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FormattedFilteredCash))]
    private decimal _filteredCashTotal;
    public string FormattedFilteredCash => $"₹{FilteredCashTotal:N0}";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FormattedFilteredUpi))]
    private decimal _filteredUpiTotal;
    public string FormattedFilteredUpi => $"₹{FilteredUpiTotal:N0}";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FormattedFilteredCount))]
    private int _filteredTransactionCount;

    public string FormattedFilteredCount => $"{FilteredTransactionCount} records found";

    [ObservableProperty]
    private string _filterPeriodDescription = string.Empty;

    [ObservableProperty]
    private ObservableCollection<TransactionItem> _filteredTransactions = [];

    [ObservableProperty]
    private ObservableCollection<EarningsChartBar> _chartBars = [];

    public event Action? RequestChartRedraw;

    // --- Constructor ---
    public PaymentsViewModel()
    {
        RefreshShopSettings();

        AppServices.Database.SettingChanged += OnSettingChanged;

        var currentYear = DateTime.Today.Year;
        for (int y = currentYear - 2; y <= currentYear + 1; y++)
        {
            AvailableYears.Add(y);
        }
        SelectedYear = currentYear;

        _ = LoadPaymentsDataAsync();
        UpdateCartTotals();
    }

    private void OnSettingChanged(string key, string value)
    {
        if (key is "shop_upi_vpa" or "shop_upi_id" or "payee_name" or "shop_name")
        {
            RefreshShopSettings();
        }
    }

    public void RefreshShopSettings()
    {
        var upi = AppServices.Database.GetSetting("shop_upi_vpa")
                  ?? AppServices.Database.GetSetting("shop_upi_id", "sevadesk.csc@upi");
        if (!string.IsNullOrWhiteSpace(upi))
        {
            ShopUpiId = upi;
        }

        var payee = AppServices.Database.GetSetting("payee_name")
                    ?? AppServices.Database.GetSetting("shop_name", "SevaDesk Cyber Cafe");
        if (!string.IsNullOrWhiteSpace(payee))
        {
            PayeeName = payee;
        }

        UpdateCartTotals();
    }

    public async Task InitializeAsync()
    {
        RefreshShopSettings();
        await LoadRatesAsync();
        await LoadPaymentsDataAsync();
    }

    private async Task LoadRatesAsync()
    {
        var rates = await AppServices.ServiceRates.GetAllActiveRatesAsync();
        RateCard.Clear();
        foreach (var rate in rates)
        {
            RateCard.Add(rate);
        }
    }

    [RelayCommand]
    public void AddNewService()
    {
        RateCard.Add(new ServiceRateItem 
        { 
            ServiceName = "New Service", 
            Rate = 10, 
            Category = "Custom",
            IsEditMode = IsEditMode
        });
    }

    [RelayCommand]
    public async Task SaveRatesAsync()
    {
        var existingRates = (await AppServices.ServiceRates.GetAllRatesAsync()).ToList();
        
        foreach (var rate in RateCard)
        {
            var exists = existingRates.Any(r => r.Id == rate.Id);
            if (exists)
            {
                await AppServices.ServiceRates.UpdateRateAsync(rate);
            }
            else
            {
                await AppServices.ServiceRates.AddRateAsync(rate);
            }
        }
        
        IsEditMode = false;
        await LoadRatesAsync();
        ShowSuccess("Rate card updated successfully.");
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
                Time = p.PaymentDate.ToLocalTime(),
                ItemsSummary = p.ItemsSummary
            });
        }

        var summary = await AppServices.Payments.GetTodaySalesSummaryAsync();
        TodayTotalSales = summary.TotalSales;
        TodayCashTotal = summary.CashTotal;
        TodayUpiTotal = summary.UpiTotal;

        OnPropertyChanged(nameof(TodayTransactionCount));
        OnPropertyChanged(nameof(TodayLedgerHeaderSummary));
    }

    // --- POS Cart Operations ---
    [RelayCommand]
    public void AddToCart(ServiceRateItem service)
    {
        var existing = CartItems.FirstOrDefault(c => c.ServiceName == service.ServiceName);
        if (existing != null)
        {
            existing.Quantity += 1;
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
        ShowInfo("Current bill cleared.");
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
            Time = DateTime.Now,
            ItemsSummary = itemsSummary
        };

        RecentTransactions.Insert(0, newTx);

        var summary = await AppServices.Payments.GetTodaySalesSummaryAsync();
        TodayTotalSales = summary.TotalSales;
        TodayCashTotal = summary.CashTotal;
        TodayUpiTotal = summary.UpiTotal;

        OnPropertyChanged(nameof(TodayTransactionCount));
        OnPropertyChanged(nameof(TodayLedgerHeaderSummary));

        ShowSuccess($"Payment of ₹{GrandTotal:N0} recorded via {paymentMode} ({invoiceNo})!");

        CartItems.Clear();
        UpdateCartTotals();
        CustomerName = "Walk-in Customer";

        // Refresh earnings if tab is open
        if (IsEarningsView)
        {
            _ = LoadEarningsAsync();
        }
    }

    private void UpdateCartTotals()
    {
        GrandTotal = CartItems.Sum(c => c.Total);
        OnPropertyChanged(nameof(HasCartItems));
        var encName = Uri.EscapeDataString(string.IsNullOrWhiteSpace(PayeeName) ? "SevaDesk Cyber Cafe" : PayeeName);
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
        ShowInfo($"Pre-loaded fees from session for {CustomerName}. Total: ₹{GrandTotal:N0}");
    }

    // --- Earnings Filter Event Handlers ---
    partial void OnSelectedQuickFilterChanged(string value)
    {
        _ = LoadEarningsAsync();
    }

    partial void OnSelectedYearChanged(int value)
    {
        if (SelectedQuickFilter == "By Year")
        {
            _ = LoadEarningsAsync();
        }
    }

    partial void OnSelectedMonthIndexChanged(int value)
    {
        if (SelectedQuickFilter == "By Year")
        {
            _ = LoadEarningsAsync();
        }
    }

    partial void OnSelectedPaymentModeChanged(string value)
    {
        _ = LoadEarningsAsync();
    }

    partial void OnCustomFromDateChanged(DateTimeOffset? value)
    {
        if (SelectedQuickFilter == "Custom Range")
        {
            _ = LoadEarningsAsync();
        }
    }

    partial void OnCustomToDateChanged(DateTimeOffset? value)
    {
        if (SelectedQuickFilter == "Custom Range")
        {
            _ = LoadEarningsAsync();
        }
    }

    partial void OnSearchFilterChanged(string value)
    {
        _ = LoadEarningsAsync();
    }

    [RelayCommand]
    public void SelectQuickFilter(string filter)
    {
        SelectedQuickFilter = filter;
    }

    // --- Earnings Loading Logic ---
    [RelayCommand]
    public async Task LoadEarningsAsync()
    {
        DateTime fromDate;
        DateTime toDate;
        bool groupByMonth = false;
        string periodDesc;

        var now = DateTime.Now;

        switch (SelectedQuickFilter)
        {
            case "Today":
                fromDate = DateTime.Today;
                toDate = DateTime.Today.AddDays(1).AddTicks(-1);
                periodDesc = $"Today ({now:dd MMM yyyy})";
                break;

            case "This Week":
                int diff = (7 + (int)now.DayOfWeek - (int)DayOfWeek.Monday) % 7;
                fromDate = DateTime.Today.AddDays(-diff);
                toDate = DateTime.Today.AddDays(1).AddTicks(-1);
                periodDesc = $"This Week ({fromDate:dd MMM} - {toDate:dd MMM})";
                break;

            case "This Month":
                fromDate = new DateTime(now.Year, now.Month, 1);
                toDate = fromDate.AddMonths(1).AddTicks(-1);
                periodDesc = now.ToString("MMMM yyyy");
                break;

            case "Last Month":
                var lastMonth = now.AddMonths(-1);
                fromDate = new DateTime(lastMonth.Year, lastMonth.Month, 1);
                toDate = fromDate.AddMonths(1).AddTicks(-1);
                periodDesc = lastMonth.ToString("MMMM yyyy");
                break;

            case "By Year":
                if (SelectedMonthIndex > 0)
                {
                    fromDate = new DateTime(SelectedYear, SelectedMonthIndex, 1);
                    toDate = fromDate.AddMonths(1).AddTicks(-1);
                    periodDesc = $"{CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(SelectedMonthIndex)} {SelectedYear}";
                    groupByMonth = false;
                }
                else
                {
                    fromDate = new DateTime(SelectedYear, 1, 1);
                    toDate = new DateTime(SelectedYear, 12, 31, 23, 59, 59);
                    periodDesc = $"Year {SelectedYear} (All Months)";
                    groupByMonth = true;
                }
                break;

            case "Custom Range":
                fromDate = CustomFromDate?.Date ?? DateTime.Today.AddDays(-30);
                toDate = (CustomToDate?.Date ?? DateTime.Today).AddDays(1).AddTicks(-1);
                periodDesc = $"{fromDate:dd MMM yyyy} - {toDate:dd MMM yyyy}";
                groupByMonth = (toDate - fromDate).TotalDays > 45;
                break;

            default:
                fromDate = new DateTime(now.Year, now.Month, 1);
                toDate = fromDate.AddMonths(1).AddTicks(-1);
                periodDesc = now.ToString("MMMM yyyy");
                break;
        }

        FilterPeriodDescription = periodDesc;

        var mode = SelectedPaymentMode == "All" ? null : SelectedPaymentMode;
        var summary = await AppServices.Payments.GetEarningsSummaryAsync(fromDate, toDate, mode);
        FilteredTotalSales = summary.TotalSales;
        FilteredCashTotal = summary.CashTotal;
        FilteredUpiTotal = summary.UpiTotal;

        var payments = (await AppServices.Payments.GetPaymentsByDateRangeAsync(fromDate, toDate, mode)).ToList();

        // Filter by keyword if provided
        IEnumerable<Payment> matchingPayments = payments;
        if (!string.IsNullOrWhiteSpace(SearchFilter))
        {
            var q = SearchFilter.Trim();
            matchingPayments = payments.Where(p =>
                p.InvoiceNo.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                p.CustomerName.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                (p.ItemsSummary != null && p.ItemsSummary.Contains(q, StringComparison.OrdinalIgnoreCase))
            );
        }

        FilteredTransactions.Clear();
        foreach (var p in matchingPayments)
        {
            FilteredTransactions.Add(new TransactionItem
            {
                Id = p.Id,
                InvoiceNo = p.InvoiceNo,
                CustomerName = p.CustomerName,
                Amount = p.Amount,
                PaymentMode = p.PaymentMethod,
                Status = "Paid",
                Time = p.PaymentDate,
                ItemsSummary = p.ItemsSummary
            });
        }
        FilteredTransactionCount = FilteredTransactions.Count;

        // Build Chart Bars
        ChartBars.Clear();
        if (groupByMonth)
        {
            for (int m = 1; m <= 12; m++)
            {
                var monthPayments = payments.Where(p => p.PaymentDate.Month == m).ToList();
                var tot = monthPayments.Sum(p => p.Amount);
                var cash = monthPayments.Where(p => string.Equals(p.PaymentMethod, "Cash", StringComparison.OrdinalIgnoreCase)).Sum(p => p.Amount);
                var upi = monthPayments.Where(p => string.Equals(p.PaymentMethod, "UPI", StringComparison.OrdinalIgnoreCase)).Sum(p => p.Amount);
                var label = CultureInfo.CurrentCulture.DateTimeFormat.GetAbbreviatedMonthName(m);

                ChartBars.Add(new EarningsChartBar
                {
                    Label = label,
                    Total = tot,
                    Cash = cash,
                    Upi = upi,
                    Tooltip = $"{label}: ₹{tot:N0} (UPI: ₹{upi:N0}, Cash: ₹{cash:N0})"
                });
            }
        }
        else
        {
            if (SelectedQuickFilter == "Today")
            {
                for (int h = 8; h <= 20; h += 2)
                {
                    var slotPayments = payments.Where(p => p.PaymentDate.Hour >= h && p.PaymentDate.Hour < h + 2).ToList();
                    var tot = slotPayments.Sum(p => p.Amount);
                    var cash = slotPayments.Where(p => string.Equals(p.PaymentMethod, "Cash", StringComparison.OrdinalIgnoreCase)).Sum(p => p.Amount);
                    var upi = slotPayments.Where(p => string.Equals(p.PaymentMethod, "UPI", StringComparison.OrdinalIgnoreCase)).Sum(p => p.Amount);
                    var label = $"{h:D2}:00";

                    ChartBars.Add(new EarningsChartBar
                    {
                        Label = label,
                        Total = tot,
                        Cash = cash,
                        Upi = upi,
                        Tooltip = $"{label} - {h+2:D2}:00: ₹{tot:N0}"
                    });
                }
            }
            else
            {
                var days = (int)(toDate.Date - fromDate.Date).TotalDays + 1;
                int maxDays = Math.Min(days, 31);
                for (int d = 0; d < maxDays; d++)
                {
                    var day = fromDate.Date.AddDays(d);
                    var dayPayments = payments.Where(p => p.PaymentDate.Date == day).ToList();
                    var tot = dayPayments.Sum(p => p.Amount);
                    var cash = dayPayments.Where(p => string.Equals(p.PaymentMethod, "Cash", StringComparison.OrdinalIgnoreCase)).Sum(p => p.Amount);
                    var upi = dayPayments.Where(p => string.Equals(p.PaymentMethod, "UPI", StringComparison.OrdinalIgnoreCase)).Sum(p => p.Amount);
                    var label = day.ToString("dd MMM");

                    ChartBars.Add(new EarningsChartBar
                    {
                        Label = label,
                        Total = tot,
                        Cash = cash,
                        Upi = upi,
                        Tooltip = $"{day:dd MMM yyyy}: ₹{tot:N0} (UPI: ₹{upi:N0}, Cash: ₹{cash:N0})"
                    });
                }
            }
        }

        RequestChartRedraw?.Invoke();
    }
}
