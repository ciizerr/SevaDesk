using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SevaDesk.Core.Models;
using SevaDesk_App.Services;

namespace SevaDesk_App.ViewModels.Pages;

public partial class DashboardViewModel : StatusViewModel
{
    [ObservableProperty]
    private string _greetingText = "Good day, Operator";

    [ObservableProperty]
    private string _formattedDateText = string.Empty;

    // --- KPI 1: Today's Revenue ---
    [ObservableProperty]
    private decimal _todayTotalSales;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FormattedTodayTotalSalesDisplay))]
    private string _formattedTodayTotalSales = "₹0";

    [ObservableProperty]
    private decimal _todayCashSales;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FormattedTodayCashSalesDisplay))]
    private string _formattedTodayCashSales = "₹0";

    [ObservableProperty]
    private decimal _todayUpiSales;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FormattedTodayUpiSalesDisplay))]
    private string _formattedTodayUpiSales = "₹0";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FormattedTodayPendingDuesDisplay))]
    [NotifyPropertyChangedFor(nameof(HasPendingDues))]
    private decimal _todayPendingDues;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FormattedTodayPendingDuesDisplay))]
    private string _formattedTodayPendingDues = "₹0";

    public bool HasPendingDues => TodayPendingDues > 0;

    // --- Privacy Mode (Hide sensitive revenue from customer) ---
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FormattedTodayTotalSalesDisplay))]
    [NotifyPropertyChangedFor(nameof(FormattedTodayCashSalesDisplay))]
    [NotifyPropertyChangedFor(nameof(FormattedTodayUpiSalesDisplay))]
    [NotifyPropertyChangedFor(nameof(FormattedTodayPendingDuesDisplay))]
    [NotifyPropertyChangedFor(nameof(PrivacyToggleGlyph))]
    [NotifyPropertyChangedFor(nameof(PrivacyToggleText))]
    [NotifyPropertyChangedFor(nameof(PrivacyToggleToolTip))]
    private bool _isPrivacyMode = true;

    public string FormattedTodayTotalSalesDisplay => IsPrivacyMode ? "₹ ••••••" : FormattedTodayTotalSales;
    public string FormattedTodayCashSalesDisplay => IsPrivacyMode ? "₹ ••••" : FormattedTodayCashSales;
    public string FormattedTodayUpiSalesDisplay => IsPrivacyMode ? "₹ ••••" : FormattedTodayUpiSales;
    public string FormattedTodayPendingDuesDisplay => IsPrivacyMode ? "₹ ••••" : FormattedTodayPendingDues;

    public string PrivacyToggleGlyph => IsPrivacyMode ? "\uED1A" : "\uE7B3";
    public string PrivacyToggleText => IsPrivacyMode ? "Show Revenue" : "Hide Revenue";
    public string PrivacyToggleToolTip => IsPrivacyMode
        ? "Financial figures hidden for customer privacy • Click to reveal"
        : "Financial figures visible • Click to hide";

    partial void OnIsPrivacyModeChanged(bool value)
    {
        foreach (var item in RecentPayments)
        {
            item.IsPrivacyMasked = value;
        }
    }

    [RelayCommand]
    public void TogglePrivacyMode()
    {
        IsPrivacyMode = !IsPrivacyMode;
    }

    // --- KPI 2: Active Sessions ---
    [ObservableProperty]
    private int _activeSessionsCount;

    [ObservableProperty]
    private int _activeCount;

    [ObservableProperty]
    private int _pausedCount;

    // --- KPI 3: Today's Visits ---
    [ObservableProperty]
    private int _todayVisitsCount;

    // --- KPI 4: Pending Tasks & Docs ---
    [ObservableProperty]
    private int _pendingTasksCount;

    [ObservableProperty]
    private int _docsReadyAppsCount;

    [ObservableProperty]
    private int _totalPendingFiles;

    // --- Collections ---
    [ObservableProperty]
    private ObservableCollection<ActiveSessionItem> _activeSessions = [];

    [ObservableProperty]
    private ActiveSessionItem? _selectedSession;

    [ObservableProperty]
    private ObservableCollection<ApplicationItem> _pendingApplications = [];

    [ObservableProperty]
    private ObservableCollection<DashboardRecentPaymentItem> _recentPayments = [];

    [ObservableProperty]
    private int _todayReceiptsCount;

    // --- State Flags ---
    [ObservableProperty]
    private bool _hasActiveSessions;

    [ObservableProperty]
    private bool _hasPendingApplications;

    [ObservableProperty]
    private bool _hasRecentPayments;

    [ObservableProperty]
    private bool _isLoading;

    public DashboardViewModel()
    {
        UpdateHeaderInfo();

        AppServices.Sessions.SessionsChanged += async (s, e) =>
        {
            await LoadDashboardDataAsync();
        };

        AppServices.Applications.ApplicationsChanged += async (s, e) =>
        {
            await LoadDashboardDataAsync();
        };

        AppServices.Payments.PaymentsChanged += async (s, e) =>
        {
            await LoadDashboardDataAsync();
        };

        AppServices.Database.SettingChanged += (key, value) =>
        {
            if (key is "shop_name" or "upi_id")
            {
                UpdateHeaderInfo();
            }
        };
    }

    public void UpdateHeaderInfo()
    {
        var hour = DateTime.Now.Hour;
        string timeGreeting = hour switch
        {
            < 12 => "Good morning",
            < 17 => "Good afternoon",
            _ => "Good evening"
        };
        var shopName = AppServices.Database.GetSetting("shop_name", "SevaDesk");
        GreetingText = $"{timeGreeting}, {shopName}";
        FormattedDateText = DateTime.Now.ToString("dddd, dd MMMM yyyy");
    }

    public async Task InitializeAsync()
    {
        await LoadDashboardDataAsync();
    }

    [RelayCommand]
    public async Task LoadDashboardDataAsync()
    {
        IsLoading = true;
        try
        {
            UpdateHeaderInfo();

            // 1. Load today's sales summary
            try
            {
                var (totalSales, cashTotal, upiTotal, pendingTotal) = await AppServices.Payments.GetTodaySalesSummaryAsync();
                TodayTotalSales = totalSales;
                FormattedTodayTotalSales = $"₹{totalSales:N0}";
                TodayCashSales = cashTotal;
                FormattedTodayCashSales = $"₹{cashTotal:N0}";
                TodayUpiSales = upiTotal;
                FormattedTodayUpiSales = $"₹{upiTotal:N0}";
                TodayPendingDues = pendingTotal;
                FormattedTodayPendingDues = $"₹{pendingTotal:N0}";
            }
            catch { }

            // 2. Load active sessions & linked apps
            try
            {
                var sessions = (await AppServices.Sessions.GetActiveSessionsAsync()).ToList();
                ActiveSessions.Clear();

                int pendingFiles = 0;
                int activeCount = 0;
                int pausedCount = 0;

                foreach (var item in sessions)
                {
                    item.UpdateElapsed();
                    if (item.Customer != null)
                    {
                        var apps = await AppServices.Applications.GetByCustomerIdAsync(item.Customer.Id);
                        var activeApp = apps.FirstOrDefault(a => a.Status != "Completed");
                        if (activeApp != null)
                        {
                            try
                            {
                                await ApplicationDocumentVerifier.CheckAndAutoUpdateStatusAsync(activeApp, item.FolderPath);
                            }
                            catch { }
                        }
                        item.LinkedApplication = activeApp;
                    }

                    if (item.IsPaused) pausedCount++;
                    else activeCount++;

                    pendingFiles += item.FolderStats.UnorganisedCount;
                    ActiveSessions.Add(item);
                }

                ActiveCount = activeCount;
                PausedCount = pausedCount;
                ActiveSessionsCount = ActiveSessions.Count;
                TotalPendingFiles = pendingFiles;
                HasActiveSessions = ActiveSessionsCount > 0;

                if (SelectedSession == null || !ActiveSessions.Any(s => s.Session.Id == SelectedSession.Session.Id))
                {
                    SelectedSession = ActiveSessions.FirstOrDefault();
                }
            }
            catch { }

            // 3. Load today's completed visits count
            try
            {
                TodayVisitsCount = await AppServices.Sessions.GetTodayCompletedVisitsCountAsync();
            }
            catch { }

            // 4. Load pending applications (Docs Ready / Draft)
            try
            {
                var allApps = await AppServices.Applications.GetAllAsync();
                var pendingApps = allApps
                    .Where(a => a.Status is "Docs Ready" or "Draft")
                    .OrderBy(a => a.Status == "Docs Ready" ? 0 : 1)
                    .ThenByDescending(a => a.UpdatedAt)
                    .Take(6)
                    .ToList();

                PendingApplications.Clear();
                foreach (var app in pendingApps)
                {
                    PendingApplications.Add(app);
                }

                DocsReadyAppsCount = pendingApps.Count(a => a.Status == "Docs Ready");
                PendingTasksCount = DocsReadyAppsCount + TotalPendingFiles;
                HasPendingApplications = PendingApplications.Count > 0;
            }
            catch { }

            // 5. Load today's recent payments
            try
            {
                var allTodayPayments = (await AppServices.Payments.GetTodayPaymentsAsync(50))
                    .Where(p => p.PaymentDate.Date == DateTime.Today)
                    .OrderByDescending(p => p.PaymentDate)
                    .ToList();

                TodayReceiptsCount = allTodayPayments.Count;

                RecentPayments.Clear();
                foreach (var p in allTodayPayments.Take(10))
                {
                    RecentPayments.Add(new DashboardRecentPaymentItem(p, IsPrivacyMode));
                }
                HasRecentPayments = RecentPayments.Count > 0;
            }
            catch
            {
                TodayReceiptsCount = 0;
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public void SelectSession(ActiveSessionItem item)
    {
        SelectedSession = item;
    }

    [RelayCommand]
    public void OpenSelectedFolder()
    {
        if (SelectedSession != null)
        {
            AppServices.FolderManager.OpenFolderInExplorer(SelectedSession.FolderPath);
        }
    }

    [RelayCommand]
    public void OpenFolder(ActiveSessionItem item)
    {
        if (item != null)
        {
            AppServices.FolderManager.OpenFolderInExplorer(item.FolderPath);
        }
    }

    [RelayCommand]
    public async Task PauseSessionAsync(string sessionId)
    {
        AppServices.FileWatcher.ClearAutoRouteIfSession(sessionId);
        await AppServices.Sessions.PauseSessionAsync(sessionId);
        await LoadDashboardDataAsync();
    }

    [RelayCommand]
    public async Task ResumeSessionAsync(string sessionId)
    {
        await AppServices.Sessions.ResumeSessionAsync(sessionId);
        await LoadDashboardDataAsync();
    }

    [RelayCommand]
    public async Task CompleteSessionAsync(string sessionId)
    {
        AppServices.FileWatcher.ClearAutoRouteIfSession(sessionId);
        var sessionItem = ActiveSessions.FirstOrDefault(s => s.Session.Id == sessionId);
        if (sessionItem?.Customer != null)
        {
            var apps = await AppServices.Applications.GetByCustomerIdAsync(sessionItem.Customer.Id);
            foreach (var app in apps.Where(a => a.Status != "Completed"))
            {
                await ApplicationDocumentVerifier.CompleteApplicationAsync(app);
            }
        }
        await AppServices.Sessions.CompleteSessionAsync(sessionId);
        await LoadDashboardDataAsync();
    }

    [RelayCommand]
    public async Task DeleteSessionAsync(ActiveSessionItem item)
    {
        if (item == null) return;
        AppServices.FileWatcher.ClearAutoRouteIfSession(item.Session.Id);

        // Clean up empty scheme subfolder if created
        if (item.LinkedApplication != null && !string.IsNullOrWhiteSpace(item.LinkedApplication.Title))
        {
            AppServices.FolderManager.CleanUpEmptyApplicationSubfolder(item.FolderPath, item.LinkedApplication.Title);
        }

        await AppServices.Sessions.DeleteSessionAsync(item.Session.Id);
        if (item.Customer != null)
        {
            AppServices.FolderManager.CleanUpEmptyCustomerWorkingFolder(item.Customer.Name, item.Customer.Code);
        }
        await LoadDashboardDataAsync();
    }
}

public partial class DashboardRecentPaymentItem : ObservableObject
{
    public Payment Payment { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FormattedAmount))]
    private bool _isPrivacyMasked;

    public string CustomerName => Payment.CustomerName;
    public string InvoiceNo => Payment.InvoiceNo;
    public string PaymentMethod => Payment.PaymentMethod;
    public DateTime PaymentDate => Payment.PaymentDate;
    public string? ItemsSummary => Payment.ItemsSummary;
    public decimal Amount => Payment.Amount;
    public string FormattedAmount => IsPrivacyMasked ? "₹ ••••" : $"₹{Amount:N0}";

    public DashboardRecentPaymentItem(Payment payment, bool isPrivacyMasked)
    {
        Payment = payment;
        _isPrivacyMasked = isPrivacyMasked;
    }
}
