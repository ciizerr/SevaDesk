using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using SevaDesk_App.Views.Flyouts;
using SevaDesk_App.Views.Pages;

namespace SevaDesk_App.Services;

public sealed class SystemTrayService
{
    private static SystemTrayService? _instance;
    public static SystemTrayService Instance => _instance ??= new SystemTrayService();

    #region Win32 P/Invoke & Structs

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
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

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    private const uint NIM_ADD = 0x00000000;
    private const uint NIM_MODIFY = 0x00000001;
    private const uint NIM_DELETE = 0x00000002;

    private const uint NIF_MESSAGE = 0x00000001;
    private const uint NIF_ICON = 0x00000002;
    private const uint NIF_TIP = 0x00000004;
    private const uint NIF_INFO = 0x00000010;

    private const uint NIIF_INFO = 0x00000001;

    private const uint WM_USER = 0x0400;
    private const uint WM_TRAYICON = WM_USER + 102;

    private const uint WM_LBUTTONUP = 0x0202;
    private const uint WM_RBUTTONUP = 0x0205;
    private const uint WM_CONTEXTMENU = 0x007B;
    private const uint NIN_SELECT = WM_USER + 0;
    private const uint NIN_BALLOONUSERCLICK = WM_USER + 5;

    private const uint MF_STRING = 0x00000000;
    private const uint MF_GRAYED = 0x00000001;
    private const uint MF_DISABLED = 0x00000002;
    private const uint MF_SEPARATOR = 0x00000800;

    private const uint TPM_RIGHTBUTTON = 0x0002;
    private const uint TPM_BOTTOMALIGN = 0x0020;
    private const uint TPM_RETURNCMD = 0x0100;

    private const uint IMAGE_ICON = 1;
    private const uint LR_LOADFROMFILE = 0x00000010;
    private const uint LR_DEFAULTSIZE = 0x00000040;

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern bool Shell_NotifyIconW(uint dwMessage, ref NOTIFYICONDATA lpData);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr LoadImage(IntPtr hInst, string lpszName, uint uType, int cxDesired, int cyDesired, uint fuLoad);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern IntPtr CreatePopupMenu();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool AppendMenuW(IntPtr hMenu, uint uFlags, nuint uIDNewItem, string? lpNewItem);

    [DllImport("user32.dll")]
    private static extern bool SetMenuDefaultItem(IntPtr hMenu, uint uItem, uint fByPos);

    [DllImport("user32.dll")]
    private static extern uint TrackPopupMenuEx(IntPtr hMenu, uint uFlags, int x, int y, IntPtr hWnd, IntPtr lpTPMParams);

    [DllImport("user32.dll")]
    private static extern bool DestroyMenu(IntPtr hMenu);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("comctl32.dll", SetLastError = true)]
    private static extern bool SetWindowSubclass(IntPtr hWnd, SUBCLASSPROC pfnSubclass, UIntPtr uIdSubclass, IntPtr dwRefData);

    [DllImport("comctl32.dll", SetLastError = true)]
    private static extern bool RemoveWindowSubclass(IntPtr hWnd, SUBCLASSPROC pfnSubclass, UIntPtr uIdSubclass);

    [DllImport("comctl32.dll", SetLastError = true)]
    private static extern IntPtr DefSubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

    private delegate IntPtr SUBCLASSPROC(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, UIntPtr uIdSubclass, IntPtr dwRefData);

    #endregion

    private IntPtr _hwnd = IntPtr.Zero;
    private IntPtr _hIcon = IntPtr.Zero;
    private bool _isIconAdded = false;
    private SUBCLASSPROC? _subclassDelegate;
    private NOTIFYICONDATA _nid;
    private DispatcherQueue? _dispatcherQueue;

    public bool IsInitialized => _isIconAdded;

    public void Initialize(MainWindow mainWindow)
    {
        if (_isIconAdded) return;

        _hwnd = Win32Interop.GetWindowFromWindowId(mainWindow.AppWindow.Id);
        _dispatcherQueue = mainWindow.DispatcherQueue;

        if (_hwnd == IntPtr.Zero) return;

        // 1. Load SevaDesk icon file
        var baseDir = AppContext.BaseDirectory;
        var iconPath = Path.Combine(baseDir, "Assets", "AppIcon.ico");
        if (!File.Exists(iconPath))
        {
            iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "AppIcon.ico");
        }

        if (File.Exists(iconPath))
        {
            _hIcon = LoadImage(IntPtr.Zero, iconPath, IMAGE_ICON, 16, 16, LR_LOADFROMFILE);
            if (_hIcon == IntPtr.Zero)
            {
                _hIcon = LoadImage(IntPtr.Zero, iconPath, IMAGE_ICON, 0, 0, LR_LOADFROMFILE | LR_DEFAULTSIZE);
            }
        }

        // 2. Configure NOTIFYICONDATA
        _nid = new NOTIFYICONDATA
        {
            cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATA>(),
            hWnd = _hwnd,
            uID = 1,
            uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
            uCallbackMessage = WM_TRAYICON,
            hIcon = _hIcon,
            szTip = AppServices.Localization.GetString("Tray.DefaultTooltip") ?? "SevaDesk - Cyber Cafe & CSC Suite"
        };

        // 3. Register in System Tray
        _isIconAdded = Shell_NotifyIconW(NIM_ADD, ref _nid);

        // 4. Subclass window to capture WM_TRAYICON messages
        _subclassDelegate = SubclassWindowProc;
        SetWindowSubclass(_hwnd, _subclassDelegate, (UIntPtr)102, IntPtr.Zero);
    }

    public void ShowBalloonNotification(string title, string message)
    {
        if (!_isIconAdded || _hwnd == IntPtr.Zero) return;

        var nid = _nid;
        nid.uFlags = NIF_INFO;
        nid.szInfoTitle = title;
        nid.szInfo = message;
        nid.dwInfoFlags = NIIF_INFO;
        nid.uTimeoutOrVersion = 3500;
        Shell_NotifyIconW(NIM_MODIFY, ref nid);
    }

    public void UpdateTooltip(string? status = null)
    {
        if (!_isIconAdded || _hwnd == IntPtr.Zero) return;

        _nid.uFlags = NIF_TIP;
        _nid.szTip = string.IsNullOrWhiteSpace(status)
            ? (AppServices.Localization.GetString("Tray.DefaultTooltip") ?? "SevaDesk - Cyber Cafe & CSC Suite")
            : status;
        Shell_NotifyIconW(NIM_MODIFY, ref _nid);
    }

    public void RemoveTrayIcon()
    {
        if (_isIconAdded)
        {
            Shell_NotifyIconW(NIM_DELETE, ref _nid);
            _isIconAdded = false;
        }

        if (_subclassDelegate != null && _hwnd != IntPtr.Zero)
        {
            RemoveWindowSubclass(_hwnd, _subclassDelegate, (UIntPtr)102);
            _subclassDelegate = null;
        }

        if (_hIcon != IntPtr.Zero)
        {
            DestroyIcon(_hIcon);
            _hIcon = IntPtr.Zero;
        }
    }

    private IntPtr SubclassWindowProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, UIntPtr uIdSubclass, IntPtr dwRefData)
    {
        if (uMsg == WM_TRAYICON)
        {
            uint mouseMsg = (uint)(lParam.ToInt64() & 0xFFFF);
            if (mouseMsg == 0) mouseMsg = (uint)lParam.ToInt64();

            if (mouseMsg == WM_LBUTTONUP || (uint)lParam.ToInt64() == WM_LBUTTONUP || mouseMsg == NIN_SELECT)
            {
                OnLeftClick();
            }
            else if (mouseMsg == WM_RBUTTONUP || (uint)lParam.ToInt64() == WM_RBUTTONUP || mouseMsg == WM_CONTEXTMENU)
            {
                OnRightClick();
            }
            else if (mouseMsg == NIN_BALLOONUSERCLICK || (uint)lParam.ToInt64() == NIN_BALLOONUSERCLICK)
            {
                OnLeftClick();
            }
            return IntPtr.Zero;
        }

        return DefSubclassProc(hWnd, uMsg, wParam, lParam);
    }

    private void OnLeftClick()
    {
        _dispatcherQueue?.TryEnqueue(() =>
        {
            MainWindow.Instance?.RestoreWindow();
        });
    }

    private async void OnRightClick()
    {
        // 1. Fetch active sessions safely
        var activeSessions = (await AppServices.Sessions.GetActiveSessionsAsync()).ToList();

        // 2. Capture cursor position
        GetCursorPos(out var pt);

        // 3. Set foreground window so menu dismisses on outside click
        SetForegroundWindow(_hwnd);

        // 4. Create Win32 popup menu
        var hMenu = CreatePopupMenu();

        // Item 1001: Open SevaDesk (Bold)
        AppendMenuW(hMenu, MF_STRING, 1001, AppServices.Localization.GetString("Tray.Open") ?? "Open SevaDesk");
        SetMenuDefaultItem(hMenu, 1001, 0);

        AppendMenuW(hMenu, MF_SEPARATOR, 0, null);

        // Dynamic Active Sessions List
        if (activeSessions.Count > 0)
        {
            var headerText = string.Format(AppServices.Localization.GetString("Tray.ActiveSessionsHeader") ?? "Active Sessions ({0})", activeSessions.Count);
            AppendMenuW(hMenu, MF_GRAYED | MF_DISABLED, 0, headerText);

            for (int i = 0; i < activeSessions.Count && i < 10; i++)
            {
                var s = activeSessions[i];
                AppendMenuW(hMenu, MF_STRING, (nuint)(1100 + i), $"  ●  {s.Customer.Name} ({s.Customer.Code})");
            }
        }
        else
        {
            AppendMenuW(hMenu, MF_GRAYED | MF_DISABLED, 0, AppServices.Localization.GetString("Tray.NoActiveSessions") ?? "No Active Sessions");
        }

        AppendMenuW(hMenu, MF_STRING, 1002, AppServices.Localization.GetString("Tray.NewSession") ?? "+ Start New Session...");

        AppendMenuW(hMenu, MF_SEPARATOR, 0, null);

        AppendMenuW(hMenu, MF_STRING, 1003, AppServices.Localization.GetString("Tray.ToggleWidget") ?? "Toggle Desktop Widget");
        AppendMenuW(hMenu, MF_STRING, 1004, AppServices.Localization.GetString("Tray.Settings") ?? "Settings");

        AppendMenuW(hMenu, MF_SEPARATOR, 0, null);

        AppendMenuW(hMenu, MF_STRING, 1005, AppServices.Localization.GetString("Tray.Exit") ?? "Exit SevaDesk");

        // 5. Track menu synchronously
        uint chosenCmd = TrackPopupMenuEx(hMenu, TPM_RETURNCMD | TPM_RIGHTBUTTON | TPM_BOTTOMALIGN, pt.X, pt.Y, _hwnd, IntPtr.Zero);
        DestroyMenu(hMenu);
        PostMessage(_hwnd, 0, IntPtr.Zero, IntPtr.Zero); // Ensure menu dismisses cleanly

        if (chosenCmd == 0) return;

        // 6. Dispatch chosen action
        _dispatcherQueue?.TryEnqueue(() =>
        {
            if (chosenCmd == 1001)
            {
                MainWindow.Instance?.RestoreWindow();
            }
            else if (chosenCmd >= 1100 && chosenCmd < 1100 + activeSessions.Count)
            {
                MainWindow.Instance?.RestoreWindow();
                MainWindow.Instance?.NavigateTo(typeof(SessionsPage));
            }
            else if (chosenCmd == 1002)
            {
                _ = MainWindow.Instance?.OpenNewSessionDialogAsync();
            }
            else if (chosenCmd == 1003)
            {
                DesktopSidebarWidget.Instance.ToggleWidget();
            }
            else if (chosenCmd == 1004)
            {
                MainWindow.Instance?.RestoreWindow();
                MainWindow.Instance?.NavigateTo(typeof(SettingsPage));
            }
            else if (chosenCmd == 1005)
            {
                ExitApplication();
            }
        });
    }

    public void ExitApplication()
    {
        _dispatcherQueue?.TryEnqueue(() =>
        {
            RemoveTrayIcon();
            DesktopSidebarWidget.Instance.Close();
            IncomingFileOverlayWidget.Instance.HideWidget();
            Application.Current.Exit();
        });
    }
}
