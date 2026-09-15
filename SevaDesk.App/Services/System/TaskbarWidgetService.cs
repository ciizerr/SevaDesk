using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using SevaDesk.Core.Models;
using SevaDesk_App.Views.Flyouts;

namespace SevaDesk_App.Services;

public sealed class TaskbarWidgetService : IDisposable
{
    public static TaskbarWidgetService Instance { get; } = new();

    private IntPtr _hwnd = IntPtr.Zero;
    private IntPtr _hIcon = IntPtr.Zero;
    private WndProc? _wndProcDelegate;
    private NOTIFYICONDATA _iconData;
    private DispatcherQueue? _dispatcherQueue;
    private TaskbarWidgetFlyout? _flyoutWindow;
    private DispatcherTimer? _pollTimer;
    private bool _isInitialized;

    public string CurrentLiveTooltip { get; private set; } = "SevaDesk — Cyber Café Management";

    private const uint WM_USER = 0x0400;
    private const uint WM_TRAYICON = WM_USER + 101;
    private const uint NIM_ADD = 0x00000000;
    private const uint NIM_MODIFY = 0x00000001;
    private const uint NIM_DELETE = 0x00000002;
    private const uint NIF_MESSAGE = 0x00000001;
    private const uint NIF_ICON = 0x00000002;
    private const uint NIF_TIP = 0x00000004;
    private const uint NIF_INFO = 0x00000010;

    private const uint WM_LBUTTONUP = 0x0202;
    private const uint WM_LBUTTONDBLCLK = 0x0203;
    private const uint WM_RBUTTONUP = 0x0205;

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct NOTIFYICONDATA
    {
        public uint cbSize;
        public IntPtr hWnd;
        public uint uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;
        public uint dwState;
        public uint dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szInfo;
        public uint uTimeoutOrVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string szInfoTitle;
        public uint dwInfoFlags;
        public Guid guidItem;
        public IntPtr hBalloonIcon;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct WNDCLASSEX
    {
        public uint cbSize;
        public uint style;
        public WndProc lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        public string? lpszMenuName;
        public string lpszClassName;
        public IntPtr hIconSm;
    }

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern ushort RegisterClassEx(ref WNDCLASSEX lpwcx);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern IntPtr CreateWindowEx(
        uint dwExStyle, string lpClassName, string lpWindowName,
        uint dwStyle, int x, int y, int nWidth, int nHeight,
        IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

    [DllImport("user32.dll")]
    private static extern IntPtr DefWindowProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool DestroyWindow(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr LoadImage(IntPtr hInst, string name, uint type, int cx, int cy, uint fuLoad);

    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    private static extern bool Shell_NotifyIcon(uint dwMessage, ref NOTIFYICONDATA lpdata);

    public void Initialize(Window mainWindow)
    {
        if (_isInitialized) return;
        _isInitialized = true;

        _dispatcherQueue = mainWindow.DispatcherQueue;

        try
        {
            // Register hidden host window to receive tray callbacks
            var className = $"SevaDesk_TrayHost_{Process.GetCurrentProcess().Id}";
            _wndProcDelegate = TrayWndProc;

            var wcx = new WNDCLASSEX
            {
                cbSize = (uint)Marshal.SizeOf<WNDCLASSEX>(),
                style = 0,
                lpfnWndProc = _wndProcDelegate,
                hInstance = Process.GetCurrentProcess().Handle,
                lpszClassName = className
            };

            RegisterClassEx(ref wcx);

            // Message-only window
            _hwnd = CreateWindowEx(0, className, "SevaDeskTrayMsgHost", 0, 0, 0, 0, 0, new IntPtr(-3) /* HWND_MESSAGE */, IntPtr.Zero, wcx.hInstance, IntPtr.Zero);

            // Load AppIcon.ico
            var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
            if (File.Exists(iconPath))
            {
                _hIcon = LoadImage(IntPtr.Zero, iconPath, 1 /* IMAGE_ICON */, 16, 16, 0x00000010 /* LR_LOADFROMFILE */);
            }

            _iconData = new NOTIFYICONDATA
            {
                cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATA>(),
                hWnd = _hwnd,
                uID = 1,
                uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
                uCallbackMessage = WM_TRAYICON,
                hIcon = _hIcon,
                szTip = CurrentLiveTooltip
            };

            Shell_NotifyIcon(NIM_ADD, ref _iconData);

            // Setup polling timer to keep active session count fresh in tray tooltip
            _pollTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            _pollTimer.Tick += async (s, e) => await RefreshLiveSessionsAsync();
            _pollTimer.Start();

            // Run initial refresh
            _ = RefreshLiveSessionsAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"TaskbarWidgetService Initialize failed: {ex.Message}");
        }
    }

    public async Task RefreshLiveSessionsAsync()
    {
        try
        {
            var sessions = (await AppServices.Sessions.GetActiveSessionsAsync()).ToList();
            UpdateLiveSessions(sessions);
        }
        catch { }
    }

    public void UpdateLiveSessions(IReadOnlyList<ActiveSessionItem> sessions)
    {
        string tooltip;
        if (sessions.Count == 0)
        {
            tooltip = "SevaDesk — Cyber Café Management (0 Active)";
        }
        else if (sessions.Count == 1)
        {
            var name = sessions[0].Customer?.Name ?? "Customer";
            tooltip = $"SevaDesk — {name} (1 Active Session)";
        }
        else
        {
            var firstName = sessions[0].Customer?.Name ?? "Customer";
            tooltip = $"SevaDesk — {firstName} +{sessions.Count - 1} Active Sessions";
        }

        if (tooltip.Length > 127) tooltip = tooltip[..127];

        CurrentLiveTooltip = tooltip;
        _iconData.szTip = tooltip;
        _iconData.uFlags = NIF_TIP;
        Shell_NotifyIcon(NIM_MODIFY, ref _iconData);

        // Update open flyout if visible
        _dispatcherQueue?.TryEnqueue(() =>
        {
            _flyoutWindow?.UpdateSessions(sessions);
        });
    }

    public void ShowNotification(string title, string message)
    {
        try
        {
            _iconData.uFlags = NIF_INFO;
            _iconData.szInfoTitle = title.Length > 63 ? title[..63] : title;
            _iconData.szInfo = message.Length > 255 ? message[..255] : message;
            _iconData.dwInfoFlags = 0x00000001; // NIIF_INFO
            Shell_NotifyIcon(NIM_MODIFY, ref _iconData);
        }
        catch { }
    }

    public void ToggleFlyout()
    {
        if (_flyoutWindow != null && _flyoutWindow.IsFlyoutVisible)
        {
            _flyoutWindow.HideFlyout();
        }
        else
        {
            ShowFlyout();
        }
    }

    public void ShowFlyout()
    {
        _flyoutWindow ??= new TaskbarWidgetFlyout();
        _flyoutWindow.ShowFlyout();
    }

    public void HideFlyout()
    {
        _flyoutWindow?.HideFlyout();
    }

    private IntPtr TrayWndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == WM_TRAYICON)
        {
            var eventType = (uint)(lParam.ToInt64() & 0xFFFF);
            switch (eventType)
            {
                case WM_LBUTTONUP:
                case WM_RBUTTONUP:
                    _dispatcherQueue?.TryEnqueue(() => ToggleFlyout());
                    break;
                case WM_LBUTTONDBLCLK:
                    _dispatcherQueue?.TryEnqueue(() =>
                    {
                        HideFlyout();
                        MainWindow.Instance?.RestoreWindow();
                    });
                    break;
            }
            return IntPtr.Zero;
        }

        return DefWindowProc(hWnd, msg, wParam, lParam);
    }

    public void Dispose()
    {
        _pollTimer?.Stop();
        _iconData.uFlags = 0;
        Shell_NotifyIcon(NIM_DELETE, ref _iconData);

        if (_hwnd != IntPtr.Zero)
        {
            DestroyWindow(_hwnd);
            _hwnd = IntPtr.Zero;
        }

        _flyoutWindow?.Close();
        _flyoutWindow = null;
    }
}
