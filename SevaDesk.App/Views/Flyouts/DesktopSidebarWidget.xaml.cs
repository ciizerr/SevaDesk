using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using SevaDesk.Core.Models;
using SevaDesk_App.Services;
using SevaDesk_App.Views.Dialogs;

namespace SevaDesk_App.Views.Flyouts;

public sealed partial class DesktopSidebarWidget : Window
{
    public static DesktopSidebarWidget Instance { get; } = new DesktopSidebarWidget();

    private const int CollapsedWidthDip = 44;
    private const int CollapsedHeightDip = 140;
    private const int ExpandedWidthDip = 380;
    private const int MarginDip = 12;
    private const double AnimDurationMs = 240.0;

    private const uint SPI_GETWORKAREA = 0x0030;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint SWP_SHOWWINDOW = 0x0040;
    private const uint SWP_NOCOPYBITS = 0x0100;
    private const uint WM_SYSCOMMAND = 0x0112;
    private const int SC_MINIMIZE = 0xF020;
    private const uint WM_GETMINMAXINFO = 0x0024;
    private const uint WM_SETCURSOR = 0x0020;
    private const int IDC_HAND = 32649;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string? lpszClass, string? lpszWindow);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam, uint fuFlags, uint uTimeout, out IntPtr lpdwResult);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    [DllImport("user32.dll")]
    private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, ref RECT pvParam, uint fWinIni);

    [DllImport("user32.dll")]
    private static extern IntPtr LoadCursor(IntPtr hInstance, int lpCursorName);

    [DllImport("user32.dll")]
    private static extern IntPtr SetCursor(IntPtr hCursor);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", EntryPoint = "SetWindowLong", SetLastError = true)]
    private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

    private static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
    {
        return IntPtr.Size == 8 ? SetWindowLongPtr64(hWnd, nIndex, dwNewLong) : new IntPtr(SetWindowLong32(hWnd, nIndex, dwNewLong.ToInt32()));
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "GetWindowLong", SetLastError = true)]
    private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

    private static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex)
    {
        return IntPtr.Size == 8 ? GetWindowLongPtr64(hWnd, nIndex) : new IntPtr(GetWindowLong32(hWnd, nIndex));
    }

    [DllImport("comctl32.dll", SetLastError = true)]
    private static extern bool SetWindowSubclass(IntPtr hWnd, SUBCLASSPROC pfnSubclass, UIntPtr uIdSubclass, IntPtr dwRefData);

    [DllImport("comctl32.dll", SetLastError = true)]
    private static extern IntPtr DefSubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

    private delegate IntPtr SUBCLASSPROC(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, UIntPtr uIdSubclass, IntPtr dwRefData);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int x;
        public int y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MINMAXINFO
    {
        public POINT ptReserved;
        public POINT ptMaxSize;
        public POINT ptMaxPosition;
        public POINT ptMinTrackSize;
        public POINT ptMaxTrackSize;
    }

    private readonly ObservableCollection<ActiveSessionItem> _sessionItems = [];
    private readonly SUBCLASSPROC _subclassDelegate;
    private readonly Stopwatch _animStopwatch = new();

    private bool _isExpanded;
    private int _currentX;
    private int _currentY;
    private int _currentW;
    private int _currentH;

    private int _startX;
    private int _startY;
    private int _startW;
    private int _startH;

    private int _targetX;
    private int _targetY;
    private int _targetW;
    private int _targetH;

    private DispatcherTimer? _animTimer;

    private DesktopSidebarWidget()
    {
        InitializeComponent();

        _subclassDelegate = SubclassWindowProc;

        ExtendsContentIntoTitleBar = true;
        SessionsListView.ItemsSource = _sessionItems;

        var hwnd = Win32Interop.GetWindowFromWindowId(AppWindow.Id);

        // WS_EX_TOOLWINDOW (0x80) hides from Taskbar & Alt+Tab
        var exStyle = (long)GetWindowLongPtr(hwnd, -20);
        SetWindowLongPtr(hwnd, -20, new IntPtr(exStyle | 0x00000080));

        if (AppWindowTitleBar.IsCustomizationSupported())
        {
            AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Collapsed;
        }

        LoadShopAndUpiSettings();
        AppServices.Database.SettingChanged += OnSettingChanged;
    }

    public async void ShowSidebar()
    {
        AttachToDesktopLayer();

        _isExpanded = false;
        CollapsedPillPanel.Visibility = Visibility.Visible;
        ExpandedPanel.Visibility = Visibility.Collapsed;

        var (x, y, w, h) = GetTargetBounds(false);
        _currentX = _targetX = _startX = x;
        _currentY = _targetY = _startY = y;
        _currentW = _targetW = _startW = w;
        _currentH = _targetH = _startH = h;

        var hwnd = Win32Interop.GetWindowFromWindowId(AppWindow.Id);
        SetWindowPos(hwnd, IntPtr.Zero, _currentX, _currentY, _currentW, _currentH, SWP_NOZORDER | SWP_NOACTIVATE | SWP_SHOWWINDOW);

        AppWindow.Show();
        LoadShopAndUpiSettings();
        await LoadSessionsAsync();
    }

    private void AttachToDesktopLayer()
    {
        var hwnd = Win32Interop.GetWindowFromWindowId(AppWindow.Id);
        if (hwnd == IntPtr.Zero) return;

        try
        {
            // 1. Locate Progman & ensure WorkerW exists behind desktop icons
            IntPtr progman = FindWindow("Progman", null);
            IntPtr workerW = IntPtr.Zero;
            SendMessageTimeout(progman, 0x052C, IntPtr.Zero, IntPtr.Zero, 0x0002, 1000, out _);

            EnumWindows((topHwnd, lParam) =>
            {
                IntPtr shellDll = FindWindowEx(topHwnd, IntPtr.Zero, "SHELLDLL_DefView", null);
                if (shellDll != IntPtr.Zero)
                {
                    workerW = FindWindowEx(IntPtr.Zero, topHwnd, "WorkerW", null);
                }
                return true;
            }, IntPtr.Zero);

            IntPtr desktopTarget = workerW != IntPtr.Zero ? workerW : progman;
            if (desktopTarget != IntPtr.Zero)
            {
                // Set desktop window as owner so Win+D treats widget as part of desktop
                SetWindowLongPtr(hwnd, -8 /* GWLP_HWNDPARENT */, desktopTarget);
            }

            // Subclass to intercept SC_MINIMIZE, override min track size, and set cursor
            SetWindowSubclass(hwnd, _subclassDelegate, (UIntPtr)1, IntPtr.Zero);
        }
        catch { }
    }

    private IntPtr SubclassWindowProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, UIntPtr uIdSubclass, IntPtr dwRefData)
    {
        if (uMsg == WM_SYSCOMMAND)
        {
            int cmd = (int)(wParam.ToInt64() & 0xFFF0);
            if (cmd == SC_MINIMIZE)
            {
                // Suppress minimize message completely (from Win+D / Win+M)
                return IntPtr.Zero;
            }
        }
        else if (uMsg == WM_GETMINMAXINFO)
        {
            // Crucial: Allow window to be narrower and shorter than Windows default 136px min track size
            MINMAXINFO mmi = Marshal.PtrToStructure<MINMAXINFO>(lParam);
            mmi.ptMinTrackSize.x = 10;
            mmi.ptMinTrackSize.y = 10;
            Marshal.StructureToPtr(mmi, lParam, true);
            return IntPtr.Zero;
        }
        else if (uMsg == WM_SETCURSOR)
        {
            if (!_isExpanded)
            {
                SetCursor(LoadCursor(IntPtr.Zero, IDC_HAND));
                return new IntPtr(1);
            }
        }

        return DefSubclassProc(hWnd, uMsg, wParam, lParam);
    }

    public void ExpandSidebar()
    {
        if (_isExpanded) return;
        _isExpanded = true;

        CollapsedPillPanel.Visibility = Visibility.Collapsed;
        ExpandedPanel.Visibility = Visibility.Visible;

        var (tx, ty, tw, th) = GetTargetBounds(true);
        _targetX = tx;
        _targetY = ty;
        _targetW = tw;
        _targetH = th;

        _startX = _currentX;
        _startY = _currentY;
        _startW = _currentW;
        _startH = _currentH;

        StartAnimation();
    }

    public void CollapseSidebar()
    {
        if (!_isExpanded) return;
        _isExpanded = false;

        var (tx, ty, tw, th) = GetTargetBounds(false);
        _targetX = tx;
        _targetY = ty;
        _targetW = tw;
        _targetH = th;

        _startX = _currentX;
        _startY = _currentY;
        _startW = _currentW;
        _startH = _currentH;

        StartAnimation();
    }

    private void StartAnimation()
    {
        if (_animTimer == null)
        {
            _animTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(15) // ~60 FPS
            };
            _animTimer.Tick += AnimTimer_Tick;
        }

        _animStopwatch.Restart();
        _animTimer.Start();
    }

    private void AnimTimer_Tick(object? sender, object e)
    {
        double elapsed = _animStopwatch.Elapsed.TotalMilliseconds;
        double t = Math.Clamp(elapsed / AnimDurationMs, 0.0, 1.0);

        // Quartic Ease-Out curve: fast start with silky deceleration
        double progress = 1.0 - Math.Pow(1.0 - t, 4);

        _currentX = (int)Math.Round(_startX + (_targetX - _startX) * progress);
        _currentY = (int)Math.Round(_startY + (_targetY - _startY) * progress);
        _currentW = (int)Math.Round(_startW + (_targetW - _startW) * progress);
        _currentH = (int)Math.Round(_startH + (_targetH - _startH) * progress);

        var hwnd = Win32Interop.GetWindowFromWindowId(AppWindow.Id);
        if (hwnd != IntPtr.Zero)
        {
            SetWindowPos(hwnd, IntPtr.Zero, _currentX, _currentY, _currentW, _currentH,
                SWP_NOZORDER | SWP_NOACTIVATE | SWP_SHOWWINDOW | SWP_NOCOPYBITS);
        }

        if (t >= 1.0)
        {
            _animTimer?.Stop();
            _animStopwatch.Stop();

            _currentX = _targetX;
            _currentY = _targetY;
            _currentW = _targetW;
            _currentH = _targetH;

            if (!_isExpanded)
            {
                ExpandedPanel.Visibility = Visibility.Collapsed;
                CollapsedPillPanel.Visibility = Visibility.Visible;
            }
        }
    }

    private (int x, int y, int w, int h) GetTargetBounds(bool expanded)
    {
        var scale = GetScale();
        var workArea = GetWorkArea();
        int workAreaH = workArea.Bottom - workArea.Top;

        if (!expanded)
        {
            int w = (int)Math.Round(CollapsedWidthDip * scale);
            int h = (int)Math.Round(CollapsedHeightDip * scale);
            int x = workArea.Right - w;
            int y = workArea.Top + (workAreaH - h) / 2;
            return (x, y, w, h);
        }
        else
        {
            int margin = (int)Math.Round(MarginDip * scale);
            int w = (int)Math.Round(ExpandedWidthDip * scale);
            int h = workAreaH - (2 * margin);
            int x = workArea.Right - w - margin;
            int y = workArea.Top + margin;
            return (x, y, w, h);
        }
    }

    private double GetScale()
    {
        var hwnd = Win32Interop.GetWindowFromWindowId(AppWindow.Id);
        var dpi = GetDpiForWindow(hwnd);
        return (dpi == 0 ? 96.0 : dpi) / 96.0;
    }

    private RECT GetWorkArea()
    {
        RECT rect = new();
        SystemParametersInfo(SPI_GETWORKAREA, 0, ref rect, 0);
        if (rect.Right == 0 && rect.Bottom == 0)
        {
            rect.Right = GetSystemMetrics(0);
            rect.Bottom = GetSystemMetrics(1);
        }
        return rect;
    }

    private void CollapsedPill_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        ExpandSidebar();
    }

    private void CollapsedPill_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        CollapsedPillPanel.Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"];
        PillChevronBorder.Background = (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"];
        if (PillChevronBorder.Child is FontIcon icon)
        {
            icon.Foreground = (Brush)Application.Current.Resources["TextOnAccentFillColorPrimaryBrush"];
        }
    }

    private void CollapsedPill_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        CollapsedPillPanel.Background = (Brush)Application.Current.Resources["LayerOnAcrylicFillColorDefaultBrush"];
        PillChevronBorder.Background = (Brush)Application.Current.Resources["SubtleFillColorSecondaryBrush"];
        if (PillChevronBorder.Child is FontIcon icon)
        {
            icon.Foreground = (Brush)Application.Current.Resources["AccentTextFillColorPrimaryBrush"];
        }
    }

    private void CollapseButton_Click(object sender, RoutedEventArgs e)
    {
        CollapseSidebar();
    }

    public async Task LoadSessionsAsync()
    {
        try
        {
            var sessions = (await AppServices.Sessions.GetActiveSessionsAsync()).ToList();
            UpdateSessions(sessions);
        }
        catch { }
    }

    public void UpdateSessions(IReadOnlyList<ActiveSessionItem> sessions)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            _sessionItems.Clear();
            foreach (var item in sessions)
            {
                _sessionItems.Add(item);
            }

            int count = _sessionItems.Count;
            TxtActiveCount.Text = $"{count} Active";
            TxtCollapsedActiveCount.Text = count.ToString();
            EmptyStatePanel.Visibility = count == 0 ? Visibility.Visible : Visibility.Collapsed;
            SessionsListView.Visibility = count > 0 ? Visibility.Visible : Visibility.Collapsed;

            if (count > 0)
            {
                CollapsedActiveBadge.Background = (Brush)Application.Current.Resources["SystemFillColorSuccessBackgroundBrush"];
                CollapsedActiveBadge.BorderBrush = (Brush)Application.Current.Resources["SystemFillColorSuccessBrush"];
                TxtCollapsedActiveCount.Foreground = (Brush)Application.Current.Resources["SystemFillColorSuccessBrush"];
            }
            else
            {
                CollapsedActiveBadge.Background = (Brush)Application.Current.Resources["SubtleFillColorSecondaryBrush"];
                CollapsedActiveBadge.BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"];
                TxtCollapsedActiveCount.Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"];
            }
        });
    }

    private void OpenFullApp_Click(object sender, RoutedEventArgs e)
    {
        MainWindow.Instance?.RestoreWindow();
    }

    private Customer? _selectedQuickCustomer;
    private CancellationTokenSource? _quickSearchCts;

    private async void EndSessionItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string sessionId)
        {
            var item = _sessionItems.FirstOrDefault(s => s.Session.Id == sessionId);
            var customer = item?.Customer;

            if (customer != null && (string.IsNullOrWhiteSpace(customer.Mobile) || string.IsNullOrWhiteSpace(customer.IdReference)))
            {
                var dialog = new CompleteSessionDialog(customer)
                {
                    XamlRoot = this.Content.XamlRoot
                };

                var res = await dialog.ShowAsync();
                if (res == ContentDialogResult.Primary)
                {
                    dialog.ApplyToCustomer(customer);
                    await AppServices.Customers.UpdateAsync(customer);
                }
                else if (res != ContentDialogResult.Secondary)
                {
                    // User clicked cancel
                    return;
                }
            }

            await AppServices.Sessions.CompleteSessionAsync(sessionId);
            await LoadSessionsAsync();
        }
    }

    private async void TxtQuickCustomerName_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
        {
            var text = sender.Text?.Trim();
            if (_selectedQuickCustomer != null && !string.Equals(_selectedQuickCustomer.Name, text, StringComparison.OrdinalIgnoreCase))
            {
                _selectedQuickCustomer = null;
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                sender.ItemsSource = null;
                return;
            }

            _quickSearchCts?.Cancel();
            _quickSearchCts = new CancellationTokenSource();
            var token = _quickSearchCts.Token;

            try
            {
                await Task.Delay(150, token);
                if (token.IsCancellationRequested) return;

                var results = await AppServices.Customers.SearchAsync(text);
                if (!token.IsCancellationRequested)
                {
                    sender.ItemsSource = results.ToList();
                }
            }
            catch (TaskCanceledException)
            {
            }
        }
    }

    private void TxtQuickCustomerName_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
    {
        if (args.SelectedItem is Customer customer)
        {
            _selectedQuickCustomer = customer;
            sender.Text = customer.Name;
        }
    }

    private async void TxtQuickCustomerName_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        if (args.ChosenSuggestion is Customer customer)
        {
            _selectedQuickCustomer = customer;
        }
        await ExecuteQuickStartAsync();
    }

    private async void QuickStart_Click(object sender, RoutedEventArgs e)
    {
        await ExecuteQuickStartAsync();
    }

    private async Task ExecuteQuickStartAsync()
    {
        var name = TxtQuickCustomerName.Text?.Trim();
        if (string.IsNullOrWhiteSpace(name)) return;

        Customer? customer = _selectedQuickCustomer;

        if (customer == null)
        {
            var matches = (await AppServices.Customers.SearchAsync(name)).ToList();
            customer = matches.FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        if (customer == null)
        {
            customer = await AppServices.Customers.CreateAsync(new Customer
            {
                Name = name
            });
        }

        TxtQuickCustomerName.Text = string.Empty;
        _selectedQuickCustomer = null;

        var session = await AppServices.Sessions.StartSessionAsync(customer.Id);
        var folder = AppServices.FolderManager.EnsureCustomerWorkingFolder(customer.Name, customer.Code);
        session.FolderPath = folder;
        session.Customer = customer;
        session.FolderStats = new FolderStats();

        await LoadSessionsAsync();
    }

    private void ToggleUpiCard_Click(object sender, RoutedEventArgs e)
    {
        UpiCardPanel.Visibility = UpiCardPanel.Visibility == Visibility.Visible
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    private void OnSettingChanged(string key, string value)
    {
        if (key is "shop_name" or "shop_upi_vpa" or "shop_upi_id" or "payee_name")
        {
            DispatcherQueue?.TryEnqueue(LoadShopAndUpiSettings);
        }
    }

    private void LoadShopAndUpiSettings()
    {
        var shopName = AppServices.Database.GetSetting("shop_name", "SevaDesk");
        var upiVpa = AppServices.Database.GetSetting("shop_upi_vpa")
                     ?? AppServices.Database.GetSetting("shop_upi_id", "sevadesk.csc@upi");
        var payeeName = AppServices.Database.GetSetting("payee_name", "SevaDesk Cyber Center");

        if (TxtShopName != null)
        {
            TxtShopName.Text = string.IsNullOrWhiteSpace(shopName) ? "SevaDesk" : shopName;
        }
        if (TxtUpiVpa != null)
        {
            TxtUpiVpa.Text = string.IsNullOrWhiteSpace(upiVpa) ? "sevadesk.csc@upi" : upiVpa;
        }
        if (TxtPayeeName != null)
        {
            TxtPayeeName.Text = string.IsNullOrWhiteSpace(payeeName) ? "SevaDesk Cyber Center" : payeeName;
        }
    }

    private void CopyUpi_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var text = TxtUpiVpa?.Text;
            if (string.IsNullOrWhiteSpace(text))
            {
                text = AppServices.Database.GetSetting("shop_upi_vpa")
                       ?? AppServices.Database.GetSetting("shop_upi_id", "sevadesk.csc@upi");
            }
            var dataPackage = new DataPackage();
            dataPackage.SetText(text);
            Clipboard.SetContent(dataPackage);
        }
        catch { }
    }
}
