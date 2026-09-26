using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SevaDesk.Core.Models;
using SevaDesk_App.Services;

namespace SevaDesk_App.ViewModels.Pages;

public partial class PaymentsViewModel : StatusViewModel
{
    public static PaymentsViewModel SharedInstance { get; } = new();

    // --- View Switching (POS vs Service Rates vs Earnings Report) ---
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPosView))]
    [NotifyPropertyChangedFor(nameof(IsRatesView))]
    [NotifyPropertyChangedFor(nameof(IsEarningsView))]
    private int _selectedViewIndex = 0;

    public bool IsPosView => SelectedViewIndex == 0;
    public bool IsRatesView => SelectedViewIndex == 1;
    public bool IsEarningsView => SelectedViewIndex == 2;

    partial void OnSelectedViewIndexChanged(int value)
    {
        if (value == 2)
        {
            _ = LoadEarningsAsync();
        }
    }

    [RelayCommand]
    public void SwitchToRatesView() => SelectedViewIndex = 1;

    [RelayCommand]
    public void SwitchToPosView() => SelectedViewIndex = 0;

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
    private ObservableCollection<ServiceRateItem> _filteredRateCard = [];

    [ObservableProperty]
    private ObservableCollection<string> _serviceCategories = [];

    [ObservableProperty]
    private string _selectedCategory = "All";

    [ObservableProperty]
    private string _rateCardSearchQuery = string.Empty;

    partial void OnSelectedCategoryChanged(string value) => ApplyRateCardFilter();
    partial void OnRateCardSearchQueryChanged(string value) => ApplyRateCardFilter();

    // --- Service Rates Catalog Tab Properties ---
    [ObservableProperty]
    private ObservableCollection<ServiceRateItem> _catalogFilteredRates = [];

    [ObservableProperty]
    private string _catalogSearchQuery = string.Empty;

    [ObservableProperty]
    private string _catalogSelectedCategory = "All";

    partial void OnCatalogSearchQueryChanged(string value) => ApplyRateCardFilter();
    partial void OnCatalogSelectedCategoryChanged(string value) => ApplyRateCardFilter();

    public string CatalogRatesSummary => $"{RateCard.Count} Total Services";

    public ObservableCollection<string> PresetCategories { get; } =
    [
        "Printing",
        "Scanning",
        "Finishing",
        "Cards",
        "Services",
        "Blank Forms",
        "Affidavits",
        "Computer / Online"
    ];

    public ObservableCollection<string> PresetUnits { get; } =
    [
        "page",
        "copy",
        "doc",
        "card",
        "sheet",
        "application",
        "form",
        "photo",
        "hour",
        "item"
    ];

    // --- Service Rates Form State ---
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEditingExistingRate))]
    [NotifyPropertyChangedFor(nameof(FormHeaderTitle))]
    [NotifyPropertyChangedFor(nameof(FormHeaderSubtitle))]
    [NotifyPropertyChangedFor(nameof(FormSaveButtonText))]
    private string? _editingRateId;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FormHeaderSubtitle))]
    private string _formServiceName = string.Empty;

    [ObservableProperty]
    private string _formCategory = "Printing";

    [ObservableProperty]
    private double _formRate = 10;

    [ObservableProperty]
    private string _formUnit = "page";

    [ObservableProperty]
    private string _formGlyph = "\uE749";

    public bool IsEditingExistingRate => !string.IsNullOrEmpty(EditingRateId);
    public string FormHeaderTitle => IsEditingExistingRate ? "Edit Service Rate" : "Add New Service";
    public string FormHeaderSubtitle => IsEditingExistingRate ? $"Updating '{FormServiceName}'" : "Configure pricing, category, and unit";
    public string FormSaveButtonText => IsEditingExistingRate ? "Update Rate" : "Save Service";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCartItems))]
    [NotifyPropertyChangedFor(nameof(DraftItemsCountText))]
    private ObservableCollection<CartItem> _cartItems = [];

    public bool HasCartItems => CartItems.Count > 0;
    public string DraftItemsCountText => CartItems.Count == 1 ? "Draft: 1 item" : $"Draft: {CartItems.Count} items";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FormattedSubTotal))]
    private decimal _subTotal;

    public string FormattedSubTotal => $"₹{SubTotal:N0}";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CalculatedDiscount))]
    [NotifyPropertyChangedFor(nameof(FormattedDiscount))]
    [NotifyPropertyChangedFor(nameof(HasDiscount))]
    [NotifyPropertyChangedFor(nameof(DiscountSummaryText))]
    [NotifyPropertyChangedFor(nameof(FormattedGrandTotal))]
    private decimal _discountValue;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CalculatedDiscount))]
    [NotifyPropertyChangedFor(nameof(FormattedDiscount))]
    [NotifyPropertyChangedFor(nameof(DiscountTypeSymbol))]
    [NotifyPropertyChangedFor(nameof(DiscountPlaceholder))]
    [NotifyPropertyChangedFor(nameof(DiscountSummaryText))]
    [NotifyPropertyChangedFor(nameof(FormattedGrandTotal))]
    private bool _isPercentageDiscount;

    public string DiscountPlaceholder => IsPercentageDiscount ? "Discount percent (e.g. 10)" : "Discount amount in ₹ (e.g. 20)";

    [ObservableProperty]
    private string _discountInputText = string.Empty;

    partial void OnDiscountInputTextChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            if (DiscountValue != 0)
            {
                DiscountValue = 0;
            }
            return;
        }

        var cleaned = value.Trim().TrimStart('₹').TrimEnd('%');
        if (decimal.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out var val) ||
            decimal.TryParse(cleaned, NumberStyles.Any, CultureInfo.CurrentCulture, out val))
        {
            var clamped = Math.Max(0, val);
            if (DiscountValue != clamped)
            {
                DiscountValue = clamped;
            }
        }
        else
        {
            if (DiscountValue != 0)
            {
                DiscountValue = 0;
            }
        }
    }

    partial void OnDiscountValueChanged(decimal value)
    {
        RecalculateGrandTotal();
    }

    partial void OnIsPercentageDiscountChanged(bool value)
    {
        RecalculateGrandTotal();
    }

    public string DiscountTypeSymbol => IsPercentageDiscount ? "%" : "₹";

    public decimal CalculatedDiscount
    {
        get
        {
            if (DiscountValue <= 0 || SubTotal <= 0) return 0;
            if (IsPercentageDiscount)
            {
                var disc = (SubTotal * DiscountValue) / 100m;
                return Math.Min(SubTotal, Math.Round(disc, 2));
            }
            return Math.Min(SubTotal, DiscountValue);
        }
    }

    public string FormattedDiscount => $"-₹{CalculatedDiscount:N0}";
    public bool HasDiscount => CalculatedDiscount > 0;

    public string DiscountSummaryText => IsPercentageDiscount ? $"({DiscountValue:G29}%)" : $"(Flat ₹{DiscountValue:N0})";

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
    private string? _currentCustomerId;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLinkedActiveSession))]
    [NotifyPropertyChangedFor(nameof(LinkedSessionDurationText))]
    private string? _currentSessionId;

    public bool HasLinkedActiveSession => !string.IsNullOrEmpty(CurrentSessionId);

    public string LinkedSessionDurationText
    {
        get
        {
            if (string.IsNullOrEmpty(CurrentSessionId)) return string.Empty;
            var active = ActiveSessions.FirstOrDefault(s => s.Session.Id == CurrentSessionId);
            if (active != null)
            {
                return $"({active.ElapsedDisplay})";
            }
            return "(Session Completed)";
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMultipleActiveSessions))]
    [NotifyPropertyChangedFor(nameof(HasActiveSessions))]
    private ObservableCollection<ActiveSessionItem> _activeSessions = [];

    public bool HasActiveSessions => ActiveSessions.Count > 0;
    public bool HasMultipleActiveSessions => ActiveSessions.Count > 1;

    [ObservableProperty]
    private string _upiDeepLink = string.Empty;

    [ObservableProperty]
    private Microsoft.UI.Xaml.Media.ImageSource? _qrCodeBitmap;

    [ObservableProperty]
    private bool _isGeneratingQr;

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
        AppServices.Sessions.SessionsChanged += (s, e) =>
        {
            _ = RefreshActiveSessionsAsync();
        };

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
        await RefreshActiveSessionsAsync();
    }

    public async Task LoadRatesAsync()
    {
        var rates = await AppServices.ServiceRates.GetAllActiveRatesAsync();
        RateCard.Clear();
        foreach (var rate in rates)
        {
            RateCard.Add(rate);
        }

        // Rebuild unique category list starting with "All"
        ServiceCategories.Clear();
        ServiceCategories.Add("All");
        var distinctCategories = RateCard.Select(r => r.Category)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct(StringComparer.OrdinalIgnoreCase);
        foreach (var cat in distinctCategories)
        {
            ServiceCategories.Add(cat);
        }

        ApplyRateCardFilter();
    }

    [RelayCommand]
    public void SelectCategory(string category)
    {
        SelectedCategory = category;
    }

    public void ApplyRateCardFilter()
    {
        // 1. POS Filter
        var posQuery = RateCardSearchQuery?.Trim();
        var posSelected = SelectedCategory ?? "All";

        var posFiltered = RateCard.AsEnumerable();
        if (!string.Equals(posSelected, "All", StringComparison.OrdinalIgnoreCase))
        {
            posFiltered = posFiltered.Where(r => string.Equals(r.Category, posSelected, StringComparison.OrdinalIgnoreCase));
        }
        if (!string.IsNullOrWhiteSpace(posQuery))
        {
            posFiltered = posFiltered.Where(r =>
                r.ServiceName.Contains(posQuery, StringComparison.OrdinalIgnoreCase) ||
                r.Category.Contains(posQuery, StringComparison.OrdinalIgnoreCase));
        }

        FilteredRateCard.Clear();
        foreach (var item in posFiltered)
        {
            FilteredRateCard.Add(item);
        }

        // 2. Catalog Tab Filter
        var catQuery = CatalogSearchQuery?.Trim();
        var catSelected = CatalogSelectedCategory ?? "All";

        var catFiltered = RateCard.AsEnumerable();
        if (!string.Equals(catSelected, "All", StringComparison.OrdinalIgnoreCase))
        {
            catFiltered = catFiltered.Where(r => string.Equals(r.Category, catSelected, StringComparison.OrdinalIgnoreCase));
        }
        if (!string.IsNullOrWhiteSpace(catQuery))
        {
            catFiltered = catFiltered.Where(r =>
                r.ServiceName.Contains(catQuery, StringComparison.OrdinalIgnoreCase) ||
                r.Category.Contains(catQuery, StringComparison.OrdinalIgnoreCase));
        }

        CatalogFilteredRates.Clear();
        foreach (var item in catFiltered)
        {
            CatalogFilteredRates.Add(item);
        }

        OnPropertyChanged(nameof(CatalogRatesSummary));
    }

    [RelayCommand]
    public void SelectCatalogCategory(string category)
    {
        CatalogSelectedCategory = category;
    }

    [RelayCommand]
    public void EditRateItem(ServiceRateItem item)
    {
        EditingRateId = item.Id;
        FormServiceName = item.ServiceName;
        FormCategory = item.Category;
        FormRate = (double)item.Rate;
        FormUnit = item.Unit;
        FormGlyph = item.Glyph;
    }

    [RelayCommand]
    public void ResetRateForm()
    {
        EditingRateId = null;
        FormServiceName = string.Empty;
        FormCategory = "Printing";
        FormRate = 10;
        FormUnit = "page";
        FormGlyph = "\uE749";
    }

    [RelayCommand]
    public async Task SaveRateFormAsync()
    {
        var name = FormServiceName?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            ShowWarning("Please enter a valid service name.");
            return;
        }

        if (FormRate < 0)
        {
            ShowWarning("Service rate cannot be negative.");
            return;
        }

        var category = string.IsNullOrWhiteSpace(FormCategory) ? "General" : FormCategory.Trim();
        var unit = string.IsNullOrWhiteSpace(FormUnit) ? "page" : FormUnit.Trim();
        var glyph = string.IsNullOrWhiteSpace(FormGlyph) ? "\uE749" : FormGlyph;

        try
        {
            if (!string.IsNullOrEmpty(EditingRateId))
            {
                var existing = RateCard.FirstOrDefault(r => r.Id == EditingRateId);
                if (existing != null)
                {
                    existing.ServiceName = name;
                    existing.Rate = (decimal)FormRate;
                    existing.Category = category;
                    existing.Unit = unit;
                    existing.Glyph = glyph;

                    await AppServices.ServiceRates.UpdateRateAsync(existing);
                    ShowSuccess($"Rate for \"{name}\" updated successfully.");
                }
            }
            else
            {
                var newRate = new ServiceRateItem
                {
                    Id = Guid.NewGuid().ToString(),
                    ServiceName = name,
                    Rate = (decimal)FormRate,
                    Category = category,
                    Unit = unit,
                    Glyph = glyph,
                    IsActive = true
                };

                await AppServices.ServiceRates.AddRateAsync(newRate);
                RateCard.Add(newRate);
                ShowSuccess($"New service \"{name}\" added to catalog.");
            }

            ResetRateForm();
            await LoadRatesAsync();
        }
        catch (Exception ex)
        {
            ShowError($"Error saving service rate: {ex.Message}");
        }
    }

    [RelayCommand]
    public async Task DeleteRateItemAsync(ServiceRateItem item)
    {
        try
        {
            await AppServices.ServiceRates.DeleteRateAsync(item.Id);
            RateCard.Remove(item);

            if (EditingRateId == item.Id)
            {
                ResetRateForm();
            }

            await LoadRatesAsync();
            ShowSuccess($"\"{item.ServiceName}\" removed from catalog.");
        }
        catch (Exception ex)
        {
            ShowError($"Error deleting service: {ex.Message}");
        }
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
                OriginalRate = service.Rate,
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
        DiscountValue = 0;
        DiscountInputText = string.Empty;
        IsPercentageDiscount = false;
        UpdateCartTotals();
        CustomerName = "Walk-in Customer";
        CurrentCustomerId = null;
        CurrentSessionId = null;
        ShowInfo("Current bill cleared.");
    }

    [RelayCommand]
    public async Task CompletePaymentAsync(string paymentMode)
    {
        if (CartItems.Count == 0) return;

        var invoiceNo = await AppServices.Payments.GenerateNextInvoiceNoAsync();
        var custName = string.IsNullOrWhiteSpace(CustomerName) ? "Walk-in Customer" : CustomerName.Trim();
        
        var itemsSummaryList = new List<string>();
        foreach (var c in CartItems)
        {
            if (c.IsRateModified)
            {
                itemsSummaryList.Add($"{c.ServiceName} x{c.Quantity} @ ₹{c.Rate:N0} (orig ₹{c.OriginalRate:N0})");
            }
            else
            {
                itemsSummaryList.Add($"{c.ServiceName} x{c.Quantity} @ ₹{c.Rate:N0}");
            }
        }
        if (HasDiscount)
        {
            itemsSummaryList.Add($"[Disc: {FormattedDiscount}]");
        }
        var itemsSummary = string.Join(", ", itemsSummaryList);

        var payment = new Payment
        {
            InvoiceNo = invoiceNo,
            CustomerId = CurrentCustomerId,
            CustomerName = custName,
            SessionId = CurrentSessionId,
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
        DiscountValue = 0;
        DiscountInputText = string.Empty;
        IsPercentageDiscount = false;
        UpdateCartTotals();
        CustomerName = "Walk-in Customer";
        CurrentCustomerId = null;
        CurrentSessionId = null;

        // Refresh earnings if tab is open
        if (IsEarningsView)
        {
            _ = LoadEarningsAsync();
        }
    }

    private void UpdateCartTotals()
    {
        SubTotal = CartItems.Sum(c => c.Total);
        RecalculateGrandTotal();
        OnPropertyChanged(nameof(HasCartItems));
        OnPropertyChanged(nameof(DraftItemsCountText));
    }

    private void RecalculateGrandTotal()
    {
        OnPropertyChanged(nameof(CalculatedDiscount));
        OnPropertyChanged(nameof(FormattedDiscount));
        OnPropertyChanged(nameof(HasDiscount));
        OnPropertyChanged(nameof(DiscountSummaryText));

        GrandTotal = Math.Max(0, SubTotal - CalculatedDiscount);
        UpiDeepLink = AppServices.QrCode.BuildUpiPayload(ShopUpiId, PayeeName, GrandTotal > 0 ? GrandTotal : null, "Cyber Cafe Bill");
        RegenerateQrCode();
    }

    public void SetDiscount(decimal value, bool isPercent)
    {
        IsPercentageDiscount = isPercent;
        DiscountValue = Math.Max(0, value);
        DiscountInputText = DiscountValue > 0 ? DiscountValue.ToString("G29") : string.Empty;
        RecalculateGrandTotal();
    }

    [RelayCommand]
    public void SetDiscountType(bool isPercent)
    {
        if (IsPercentageDiscount != isPercent)
        {
            IsPercentageDiscount = isPercent;
            RecalculateGrandTotal();
        }
    }

    [RelayCommand]
    public void ClearDiscount()
    {
        DiscountValue = 0;
        DiscountInputText = string.Empty;
        RecalculateGrandTotal();
    }

    [RelayCommand]
    public void ApplyQuickDiscount(string? param)
    {
        if (string.IsNullOrWhiteSpace(param)) return;
        var p = param.Trim();
        if (p.EndsWith("%"))
        {
            if (decimal.TryParse(p.TrimEnd('%'), NumberStyles.Any, CultureInfo.InvariantCulture, out var pVal))
            {
                SetDiscount(pVal, true);
            }
        }
        else
        {
            if (decimal.TryParse(p.TrimStart('₹'), NumberStyles.Any, CultureInfo.InvariantCulture, out var fVal))
            {
                SetDiscount(fVal, false);
            }
        }
    }

    public void UpdateItemRate(CartItem item, decimal newRate)
    {
        if (newRate >= 0)
        {
            item.Rate = newRate;
            UpdateCartTotals();
        }
    }

    public void ResetItemRate(CartItem item)
    {
        item.ResetToOriginalRate();
        UpdateCartTotals();
    }

    public void UpdateItemQuantity(CartItem item, int newQuantity)
    {
        item.Quantity = Math.Max(1, newQuantity);
        var idx = CartItems.IndexOf(item);
        if (idx >= 0)
        {
            CartItems[idx] = item;
        }
        UpdateCartTotals();
    }

    public void RegenerateQrCode()
    {
        try
        {
            IsGeneratingQr = false;
            var bmp = AppServices.QrCode.GenerateQrBitmap(UpiDeepLink, pixelsPerModule: 8);
            if (bmp != null)
            {
                QrCodeBitmap = bmp;
            }
        }
        catch { }
    }

    public void ApplyBillingHandover(BillingHandoverRequest handover)
    {
        if (handover == null) return;

        bool isSameOrUnassignedCustomer =
            string.IsNullOrWhiteSpace(CurrentCustomerId) ||
            CurrentCustomerId == "walk-in" ||
            string.Equals(CurrentCustomerId, handover.CustomerId, StringComparison.OrdinalIgnoreCase) ||
            CustomerName == "Walk-in Customer" ||
            CartItems.Count == 0;

        if (!string.IsNullOrWhiteSpace(handover.CustomerName))
        {
            CustomerName = handover.CustomerName;
        }

        CurrentCustomerId = handover.CustomerId;
        CurrentSessionId = handover.SessionId;

        if (!isSameOrUnassignedCustomer)
        {
            CartItems.Clear();
        }

        foreach (var item in handover.Items)
        {
            if (item.OriginalRate == 0 && item.Rate > 0)
            {
                item.OriginalRate = item.Rate;
            }

            var existing = CartItems.FirstOrDefault(c =>
                string.Equals(c.ServiceName, item.ServiceName, StringComparison.OrdinalIgnoreCase) &&
                c.Rate == item.Rate);

            if (existing != null)
            {
                existing.Quantity += item.Quantity;
                var idx = CartItems.IndexOf(existing);
                CartItems[idx] = existing;
            }
            else
            {
                CartItems.Add(item);
            }
        }

        UpdateCartTotals();
        _ = RefreshActiveSessionsAsync();

        if (handover.Items.Count > 0)
        {
            ShowInfo($"Added session fees for {CustomerName}. Current Total: ₹{GrandTotal:N0}");
        }
        else
        {
            ShowInfo($"Ready to bill session for {CustomerName}. Total: ₹{GrandTotal:N0}");
        }
    }

    public async Task RefreshActiveSessionsAsync()
    {
        try
        {
            var sessions = (await AppServices.Sessions.GetActiveSessionsAsync()).ToList();
            ActiveSessions.Clear();
            foreach (var s in sessions)
            {
                ActiveSessions.Add(s);
            }
            OnPropertyChanged(nameof(HasActiveSessions));
            OnPropertyChanged(nameof(HasMultipleActiveSessions));
            OnPropertyChanged(nameof(HasLinkedActiveSession));
            OnPropertyChanged(nameof(LinkedSessionDurationText));
        }
        catch { }
    }

    public async Task CheckAutoSelectActiveSessionAsync()
    {
        await RefreshActiveSessionsAsync();

        bool isUnassigned = string.IsNullOrWhiteSpace(CurrentCustomerId) ||
                            CurrentCustomerId == "walk-in" ||
                            CustomerName == "Walk-in Customer";

        if (isUnassigned && ActiveSessions.Count == 1 && string.IsNullOrEmpty(CurrentSessionId))
        {
            var active = ActiveSessions[0];
            SelectActiveSession(active, autoSelected: true);
        }
    }

    [RelayCommand]
    public void SelectActiveSession(ActiveSessionItem session)
    {
        SelectActiveSession(session, autoSelected: false);
    }

    public void SelectActiveSession(ActiveSessionItem session, bool autoSelected)
    {
        if (session?.Customer == null) return;

        CustomerName = session.Customer.Name;
        CurrentCustomerId = session.Customer.Id;
        CurrentSessionId = session.Session.Id;

        OnPropertyChanged(nameof(HasLinkedActiveSession));
        OnPropertyChanged(nameof(LinkedSessionDurationText));

        if (autoSelected)
        {
            ShowInfo($"Auto-selected active session for {CustomerName}");
        }
        else
        {
            ShowSuccess($"Linked active session for {CustomerName}");
        }
    }

    [RelayCommand]
    public void UnlinkSession()
    {
        CurrentSessionId = null;
        OnPropertyChanged(nameof(HasLinkedActiveSession));
        OnPropertyChanged(nameof(LinkedSessionDurationText));
        ShowInfo("Unlinked session from bill.");
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
