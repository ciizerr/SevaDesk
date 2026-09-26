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
using SevaDesk_App.Views.Pages;

namespace SevaDesk_App.Views.Flyouts;

public sealed partial class DesktopSidebarWidget : Window
{
    public static DesktopSidebarWidget Instance { get; } = new DesktopSidebarWidget();

    private const int CollapsedWidthDip = 52;
    private const int CollapsedHeightDip = 140;
    private const int ExpandedWidthDip = 380;
    private const int MarginDip = 12;
    private const double AnimDurationMs = 180.0;

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
        AppServices.Sessions.SessionsChanged += async (s, e) =>
        {
            await LoadSessionsAsync();
        };
        AppServices.Applications.ApplicationsChanged += async (s, e) =>
        {
            await LoadSessionsAsync();
        };
        CustomerAvatarHelper.AvatarUpdated += (photo) =>
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                foreach (var item in _sessionItems)
                {
                    item.Customer?.NotifyPhotoUpdated();
                }
            });
        };
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

    public void ToggleWidget()
    {
        if (AppWindow.IsVisible)
        {
            AppWindow.Hide();
        }
        else
        {
            ShowSidebar();
        }
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

        // Reset opacity to 0 and show panel for smooth crossfade without layout jump
        ExpandedPanel.Opacity = 0.0;
        ExpandedPanel.Visibility = Visibility.Visible;
        CollapsedPillPanel.Visibility = Visibility.Collapsed;

        var (tx, ty, tw, th) = GetTargetBounds(true);
        _targetX = tx;
        _targetY = ty;
        _targetW = tw;
        _targetH = th;

        // Set vertical bounds immediately to expanded target so height does not jitter during horizontal slide
        _currentY = _targetY;
        _currentH = _targetH;
        _startY = _targetY;
        _startH = _targetH;

        _startX = _currentX;
        _startW = _currentW;

        var hwnd = Win32Interop.GetWindowFromWindowId(AppWindow.Id);
        if (hwnd != IntPtr.Zero)
        {
            SetWindowPos(hwnd, IntPtr.Zero, _startX, _targetY, _startW, _targetH,
                SWP_NOZORDER | SWP_NOACTIVATE | SWP_SHOWWINDOW);
        }

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

        // Keep current Y and H during horizontal slide; snapped cleanly to collapsed pill bounds upon completion
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
                Interval = TimeSpan.FromMilliseconds(10) // Snappy update
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
        _currentW = (int)Math.Round(_startW + (_targetW - _startW) * progress);

        // Opacity crossfade
        if (_isExpanded)
        {
            ExpandedPanel.Opacity = Math.Clamp(progress, 0.0, 1.0);
        }
        else
        {
            // Rapid fade-out on collapse so content disappears smoothly before physical window collapses
            ExpandedPanel.Opacity = Math.Clamp(1.0 - (t * 2.0), 0.0, 1.0);
        }

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

            if (hwnd != IntPtr.Zero)
            {
                SetWindowPos(hwnd, IntPtr.Zero, _currentX, _currentY, _currentW, _currentH,
                    SWP_NOZORDER | SWP_NOACTIVATE | SWP_SHOWWINDOW);
            }

            if (!_isExpanded)
            {
                ExpandedPanel.Visibility = Visibility.Collapsed;
                ExpandedPanel.Opacity = 1.0;
                CollapsedPillPanel.Visibility = Visibility.Visible;
            }
            else
            {
                ExpandedPanel.Opacity = 1.0;
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

    private DispatcherTimer? _sessionTicker;

    private void EnsureSessionTicker(bool hasSessions)
    {
        if (hasSessions)
        {
            if (_sessionTicker == null)
            {
                _sessionTicker = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
                _sessionTicker.Tick += (s, e) =>
                {
                    foreach (var item in _sessionItems)
                    {
                        item.UpdateElapsed();
                    }
                };
            }
            if (!_sessionTicker.IsEnabled)
            {
                _sessionTicker.Start();
            }
        }
        else
        {
            _sessionTicker?.Stop();
        }
    }

    public async Task LoadSessionsAsync()
    {
        try
        {
            var sessions = (await AppServices.Sessions.GetActiveSessionsAsync()).ToList();
            foreach (var s in sessions)
            {
                if (s.Customer != null)
                {
                    var apps = await AppServices.Applications.GetByCustomerIdAsync(s.Customer.Id);
                    var activeApp = apps.FirstOrDefault(a => a.Status != "Completed");
                    if (activeApp != null)
                    {
                        try
                        {
                            await ApplicationDocumentVerifier.CheckAndAutoUpdateStatusAsync(activeApp, s.FolderPath);
                        }
                        catch { }
                    }
                    s.LinkedApplication = activeApp;
                }
            }
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
                item.UpdateElapsed();
                _sessionItems.Add(item);
            }

            int count = _sessionItems.Count;
            TxtActiveCount.Text = $"{count} Active";
            TxtCollapsedActiveCount.Text = count.ToString();
            EmptyStatePanel.Visibility = count == 0 ? Visibility.Visible : Visibility.Collapsed;
            SessionsListView.Visibility = count > 0 ? Visibility.Visible : Visibility.Collapsed;

            EnsureSessionTicker(count > 0);

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

                if (_isExpanded)
                {
                    CollapseSidebar();
                }
            }
        });
    }

    private void OpenFullApp_Click(object sender, RoutedEventArgs e)
    {
        MainWindow.Instance?.RestoreWindow();
    }

    private async void TogglePauseSession_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string sessionId)
        {
            var item = _sessionItems.FirstOrDefault(s => s.Session.Id == sessionId);
            if (item != null)
            {
                if (item.Session.Status == "Active")
                {
                    var startedUtc = item.Session.StartedAt.Kind == DateTimeKind.Utc
                        ? item.Session.StartedAt
                        : (item.Session.StartedAt.Kind == DateTimeKind.Unspecified
                            ? DateTime.SpecifyKind(item.Session.StartedAt, DateTimeKind.Utc)
                            : item.Session.StartedAt.ToUniversalTime());
                    var activeSec = Math.Max(0, (int)(DateTime.UtcNow - startedUtc).TotalSeconds);
                    item.Session.DurationSeconds += activeSec;
                    item.Session.Status = "Paused";

                    AppServices.FileWatcher.ClearAutoRouteIfSession(item.Session.Id);
                    await AppServices.Sessions.PauseSessionAsync(item.Session.Id, item.Session.DurationSeconds);
                }
                else
                {
                    item.Session.StartedAt = DateTime.UtcNow;
                    item.Session.Status = "Active";

                    await AppServices.Sessions.ResumeSessionAsync(item.Session.Id);
                }

                item.NotifyStatusChanged();
                item.UpdateElapsed();
            }
        }
    }

    private async void OpenLinkFormFlyout_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string sessionId)
        {
            var item = _sessionItems.FirstOrDefault(s => s.Session.Id == sessionId);
            if (item?.Customer == null) return;

            var templates = (await AppServices.Applications.GetAllTemplatesAsync()).ToList();
            if (templates.Count == 0)
            {
                ToastService.Instance.ShowInfo("No form templates available in catalog");
                return;
            }

            var flyout = new MenuFlyout();
            foreach (var tmpl in templates)
            {
                var feeText = tmpl.DefaultServiceFee > 0 ? $" (₹{tmpl.DefaultServiceFee:N0})" : "";
                var mfi = new MenuFlyoutItem
                {
                    Text = $"{tmpl.Title}{feeText}",
                    Icon = new FontIcon { Glyph = "\uE7C3" }
                };
                mfi.Click += async (s, args) =>
                {
                    await LinkTemplateToSessionAsync(item, tmpl);
                };
                flyout.Items.Add(mfi);
            }

            flyout.ShowAt(btn);
        }
    }

    public static Brush SessionStatusBackground(bool isPaused) =>
        isPaused
            ? (Brush)Application.Current.Resources["SystemFillColorAttentionBackgroundBrush"]
            : (Brush)Application.Current.Resources["SystemFillColorSuccessBackgroundBrush"];

    public static Brush SessionStatusForeground(bool isPaused) =>
        isPaused
            ? (Brush)Application.Current.Resources["SystemFillColorCautionBrush"]
            : (Brush)Application.Current.Resources["SystemFillColorSuccessBrush"];

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string sessionId)
        {
            var item = _sessionItems.FirstOrDefault(s => s.Session.Id == sessionId);
            if (item != null)
            {
                var folderPath = !string.IsNullOrWhiteSpace(item.FolderPath)
                    ? item.FolderPath
                    : (item.Customer != null
                        ? AppServices.FolderManager.EnsureCustomerWorkingFolder(item.Customer.Name, item.Customer.Code)
                        : null);

                if (!string.IsNullOrWhiteSpace(folderPath))
                {
                    AppServices.FolderManager.OpenFolderInExplorer(folderPath);
                }
            }
        }
    }

    private void OpenCustomerWorkspace_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem item && item.Tag is string sessionId)
        {
            var sessionItem = _sessionItems.FirstOrDefault(s => s.Session.Id == sessionId);
            if (sessionItem?.Customer == null) return;

            MainWindow.Instance?.RestoreWindow();
            MainWindow.Instance?.NavigateTo(typeof(CustomerWorkspacePage), sessionItem.Customer);
        }
    }

    private async void DeleteSession_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem item && item.Tag is string sessionId)
        {
            var sessionItem = _sessionItems.FirstOrDefault(s => s.Session.Id == sessionId);
            if (sessionItem == null) return;

            MainWindow.Instance?.RestoreWindow();
            var mainRoot = MainWindow.Instance?.Content?.XamlRoot;
            if (mainRoot == null) return;

            var (action, fileCount) = await AppServices.Dialogs.PromptDeleteActiveSessionAsync(sessionItem, mainRoot);
            if (action == DeleteSessionFileAction.Cancel) return;

            if (action == DeleteSessionFileAction.BackupAndDelete && sessionItem.Customer != null)
            {
                try
                {
                    await AppServices.FolderManager.SyncToBackupAsync(sessionItem.Customer.Name, sessionItem.Customer.Code);
                    AppServices.FolderManager.DeleteCustomerWorkingFolder(sessionItem.Customer.Name, sessionItem.Customer.Code);
                }
                catch { }
            }

            AppServices.FileWatcher.ClearAutoRouteIfSession(sessionItem.Session.Id);
            await AppServices.Sessions.DeleteSessionAsync(sessionItem.Session.Id);

            if (action == DeleteSessionFileAction.BackupAndDelete)
            {
                MainWindow.Instance?.ShowToast(
                    fileCount > 0
                        ? $"Session deleted. {fileCount} file(s) backed up to permanent archive and removed from Desktop."
                        : "Session deleted and Desktop folder cleaned up.",
                    InfoBarSeverity.Success);
            }
            else if (fileCount > 0)
            {
                MainWindow.Instance?.ShowToast(
                    "Session deleted. Files kept on Desktop.",
                    InfoBarSeverity.Informational);
            }
            else
            {
                MainWindow.Instance?.ShowToast(
                    AppServices.Localization.GetString("Toast.DeleteSuccess", "Session deleted"),
                    InfoBarSeverity.Success);
            }

            await LoadSessionsAsync();
        }
    }

    public static Brush AppStatusBackground(string? status) =>
        status switch
        {
            "Docs Ready" or "Docs Uploaded" => new SolidColorBrush(Windows.UI.Color.FromArgb(35, 59, 130, 246)),
            "Completed" => new SolidColorBrush(Windows.UI.Color.FromArgb(35, 16, 185, 129)),
            _ => new SolidColorBrush(Windows.UI.Color.FromArgb(35, 245, 158, 11))
        };

    public static Brush AppStatusForeground(string? status) =>
        status switch
        {
            "Docs Ready" or "Docs Uploaded" => new SolidColorBrush(Windows.UI.Color.FromArgb(255, 59, 130, 246)),
            "Completed" => new SolidColorBrush(Windows.UI.Color.FromArgb(255, 16, 185, 129)),
            _ => new SolidColorBrush(Windows.UI.Color.FromArgb(255, 217, 119, 6))
        };

    public static string AppStatusDisplay(string? status) =>
        status switch
        {
            "Docs Ready" or "Docs Uploaded" => "Docs Ready",
            "Completed" => "Completed",
            _ => "Draft"
        };

    private async Task LinkTemplateToSessionAsync(ActiveSessionItem item, ApplicationTemplate tmpl)
    {
        var app = new ApplicationItem
        {
            CustomerId = item.Customer.Id,
            SessionId = item.Session.Id,
            CustomerName = item.Customer.Name,
            Title = tmpl.Title,
            PortalName = tmpl.PortalUrl,
            ApplicationNumber = $"REG-{DateTime.Now:yyyyMMdd}-{DateTime.Now.Millisecond}",
            Status = "Draft",
            ServiceCharge = tmpl.DefaultServiceFee,
            GovtFee = tmpl.DefaultGovtFee,
            RequiredDocs = tmpl.RequiredDocs,
            Notes = tmpl.Notes,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var created = await AppServices.Applications.CreateAsync(app);
        item.LinkedApplication = created;

        // Auto-check existing files in customer working folder & backup folder
        await ApplicationDocumentVerifier.CheckAndAutoUpdateStatusAsync(created, item.FolderPath);
        item.NotifyLinkedApplicationChanged();

        ToastService.Instance.ShowSuccess($"Linked '{created.Title}' to {item.Customer.Name}");
    }

    private async void UnlinkApplication_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string sessionId)
        {
            var item = _sessionItems.FirstOrDefault(s => s.Session.Id == sessionId);
            if (item?.LinkedApplication != null)
            {
                var appTitle = item.LinkedApplication.Title;
                await AppServices.Applications.DeleteAsync(item.LinkedApplication.Id);
                item.LinkedApplication = null;
                ToastService.Instance.ShowInfo($"Removed '{appTitle}'");
            }
        }
    }

    private Customer? _selectedQuickCustomer;
    private CancellationTokenSource? _quickSearchCts;

    private async void EndSessionItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string sessionId)
        {
            var item = _sessionItems.FirstOrDefault(s => s.Session.Id == sessionId);
            if (item == null) return;
            var customer = item.Customer;

            // 1. Restore MainWindow
            MainWindow.Instance?.RestoreWindow();

            // 2. Prompt for missing customer info on MainWindow if applicable
            if (customer != null && (string.IsNullOrWhiteSpace(customer.Mobile) || string.IsNullOrWhiteSpace(customer.IdReference)))
            {
                var mainRoot = MainWindow.Instance?.Content?.XamlRoot;
                if (mainRoot != null)
                {
                    var dialog = new CompleteSessionDialog(customer)
                    {
                        XamlRoot = mainRoot
                    };

                    var res = await dialog.ShowAsync();
                    if (res == ContentDialogResult.Primary)
                    {
                        dialog.ApplyToCustomer(customer);
                        await AppServices.Customers.UpdateAsync(customer);
                    }
                    else if (res != ContentDialogResult.Secondary)
                    {
                        // User canceled
                        return;
                    }
                }
            }

            // 3. Prepare handover request for Billing
            var handover = new BillingHandoverRequest
            {
                CustomerId = customer?.Id,
                CustomerName = customer?.Name ?? "Customer",
                SessionId = sessionId,
                Items = []
            };

            var app = item.LinkedApplication;
            if (app != null)
            {
                await ApplicationDocumentVerifier.CompleteApplicationAsync(app);

                if (app.ServiceCharge > 0)
                {
                    handover.Items.Add(new CartItem
                    {
                        ServiceName = $"{app.Title} (Service Fee)",
                        Rate = app.ServiceCharge,
                        Quantity = 1
                    });
                }
                if (app.GovtFee > 0)
                {
                    handover.Items.Add(new CartItem
                    {
                        ServiceName = $"{app.Title} (Govt Fee)",
                        Rate = app.GovtFee,
                        Quantity = 1
                    });
                }
                if (handover.Items.Count == 0)
                {
                    handover.Items.Add(new CartItem
                    {
                        ServiceName = $"{app.Title} Form Fill",
                        Rate = 100,
                        Quantity = 1
                    });
                }
            }

            // 4. Complete session in DB
            AppServices.FileWatcher.ClearAutoRouteIfSession(sessionId);
            await AppServices.Sessions.CompleteSessionAsync(sessionId);

            // 5. Navigate MainWindow to PaymentsPage with handover
            MainWindow.Instance?.NavigateTo(typeof(PaymentsPage), handover);

            // 6. Refresh widget sessions & collapse
            await LoadSessionsAsync();
            CollapseSidebar();
        }
    }

    private void StageSelectedCustomer(Customer customer)
    {
        _selectedQuickCustomer = customer;
        TxtSelectedCustomerName.Text = customer.Name;

        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(customer.Mobile)) parts.Add(customer.Mobile);
        if (!string.IsNullOrWhiteSpace(customer.Code)) parts.Add(customer.Code);
        if (!string.IsNullOrWhiteSpace(customer.Village)) parts.Add(customer.Village);
        TxtSelectedCustomerSub.Text = parts.Count > 0 ? string.Join(" · ", parts) : "Customer";

        TxtSelectedCustomerInitials.Text = customer.Initials;

        TxtQuickCustomerName.Visibility = Visibility.Collapsed;
        SelectedCustomerCard.Visibility = Visibility.Visible;
        BtnQuickStart.Focus(FocusState.Programmatic);
    }

    private void ClearSelectedCustomer_Click(object sender, RoutedEventArgs e)
    {
        _selectedQuickCustomer = null;
        SelectedCustomerCard.Visibility = Visibility.Collapsed;
        TxtQuickCustomerName.Visibility = Visibility.Visible;
        TxtQuickCustomerName.Text = string.Empty;
        TxtQuickCustomerName.ItemsSource = null;
        TxtQuickCustomerName.Focus(FocusState.Programmatic);
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
            StageSelectedCustomer(customer);
        }
    }

    private async void TxtQuickCustomerName_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        if (args.ChosenSuggestion is Customer customer)
        {
            StageSelectedCustomer(customer);
            return;
        }

        if (string.IsNullOrWhiteSpace(args.QueryText))
        {
            return;
        }

        await ExecuteQuickStartAsync();
    }

    private async void QuickStart_Click(object sender, RoutedEventArgs e)
    {
        await ExecuteQuickStartAsync();
    }

    private async Task ExecuteQuickStartAsync()
    {
        Customer? customer = _selectedQuickCustomer;

        if (customer == null)
        {
            var name = TxtQuickCustomerName.Text?.Trim();
            if (string.IsNullOrWhiteSpace(name)) return;

            var matches = (await AppServices.Customers.SearchAsync(name)).ToList();
            customer = matches.FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));

            if (customer == null)
            {
                customer = await AppServices.Customers.CreateAsync(new Customer
                {
                    Name = name
                });
            }
        }

        // Reset staged customer UI & search box
        _selectedQuickCustomer = null;
        SelectedCustomerCard.Visibility = Visibility.Collapsed;
        TxtQuickCustomerName.Visibility = Visibility.Visible;
        TxtQuickCustomerName.Text = string.Empty;
        TxtQuickCustomerName.ItemsSource = null;

        var session = await AppServices.Sessions.StartSessionAsync(customer.Id);
        var folder = AppServices.FolderManager.EnsureCustomerWorkingFolder(customer.Name, customer.Code);
        session.FolderPath = folder;
        session.Customer = customer;
        session.FolderStats = new FolderStats();

        await LoadSessionsAsync();
    }

    private void ToggleUpiCard_Click(object sender, RoutedEventArgs e)
    {
        var isOpening = UpiCardPanel.Visibility != Visibility.Visible;
        UpiCardPanel.Visibility = isOpening ? Visibility.Visible : Visibility.Collapsed;
        if (isOpening)
        {
            LoadShopAndUpiSettings();
        }
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

        if (ImgWidgetQrCode != null)
        {
            try
            {
                var payload = AppServices.QrCode.BuildUpiPayload(upiVpa ?? "sevadesk.csc@upi", payeeName ?? "SevaDesk Cyber Center", null, "Cyber Cafe Services");
                var bmp = AppServices.QrCode.GenerateQrBitmap(payload, pixelsPerModule: 8);
                if (bmp != null)
                {
                    ImgWidgetQrCode.Source = bmp;
                }
            }
            catch { }
            finally
            {
                if (WidgetQrLoadingRing != null)
                {
                    WidgetQrLoadingRing.IsActive = false;
                    WidgetQrLoadingRing.Visibility = Visibility.Collapsed;
                }
            }
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
