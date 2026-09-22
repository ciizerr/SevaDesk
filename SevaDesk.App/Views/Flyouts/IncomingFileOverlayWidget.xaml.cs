using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Windows.Graphics;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SevaDesk.Core.Models;
using SevaDesk_App.Services;
using SevaDesk_App.ViewModels.Pages;
using SevaDesk_App.Views.Pages;

namespace SevaDesk_App.Views.Flyouts;

public sealed partial class IncomingFileOverlayWidget : Window
{
    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    [StructLayout(LayoutKind.Sequential)]
    private struct APPBARDATA
    {
        public uint cbSize;
        public IntPtr hWnd;
        public uint uCallbackMessage;
        public uint uEdge;
        public RECT rc;
        public IntPtr lParam;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    private const uint ABM_GETTASKBARPOS = 0x00000005;
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    
    private const int DWMWA_BORDER_COLOR = 34;
    private const int DWMWA_COLOR_NONE = unchecked((int)0xFFFFFFFE);

    [DllImport("shell32.dll")]
    private static extern IntPtr SHAppBarMessage(uint dwMessage, ref APPBARDATA pData);

    private static IncomingFileOverlayWidget? _instance;
    public static IncomingFileOverlayWidget Instance => _instance ??= new IncomingFileOverlayWidget();

    private IncomingFileItem? _currentFile;
    private readonly DispatcherTimer _countdownTimer = new();
    private double _remainingSeconds = 12.0;
    private const double TotalSeconds = 12.0;

    public bool IsWidgetVisible { get; private set; }

    public IncomingFileOverlayWidget()
    {
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;

        var hwnd = Win32Interop.GetWindowFromWindowId(AppWindow.Id);
        var exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_TOOLWINDOW);

        int colorNone = DWMWA_COLOR_NONE;
        DwmSetWindowAttribute(hwnd, DWMWA_BORDER_COLOR, ref colorNone, sizeof(int));

        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsAlwaysOnTop = true;
            presenter.IsResizable = false;
            presenter.SetBorderAndTitleBar(false, false);
        }

        _countdownTimer.Interval = TimeSpan.FromMilliseconds(100);
        _countdownTimer.Tick += CountdownTimer_Tick;
    }

    public async void ShowForFile(IncomingFileItem file)
    {
        _currentFile = file;
        _remainingSeconds = TotalSeconds;
        TimeoutProgressBar.Value = 100;

        // Populate File Details
        TxtFileName.Text = file.FileName;
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        FileGlyphIcon.Glyph = ext switch
        {
            ".pdf" => "\uE8A5",
            ".jpg" or ".jpeg" or ".png" or ".webp" or ".bmp" => "\uEB9F",
            ".doc" or ".docx" or ".txt" => "\uE8C1",
            _ => "\uE8A5"
        };

        string sizeStr = file.FileSize > 1024 * 1024
            ? $"{file.FileSize / (1024.0 * 1024.0):F1} MB"
            : $"{Math.Max(1, file.FileSize / 1024)} KB";
        var sourceName = Path.GetFileName(file.SourceFolder);
        TxtFileMeta.Text = $"{sourceName} • {sizeStr}";

        // Configure Action Button according to active customer sessions
        var activeSessions = (await AppServices.Sessions.GetActiveSessionsAsync()).ToList();

        if (activeSessions.Count == 1)
        {
            var single = activeSessions[0];
            BtnMainAction.Flyout = null;
            TxtMainAction.Text = string.Format(AppServices.Localization.GetString("Triage.MoveToSingle") ?? "Move to {0}", single.Customer.Name);
            BtnMainAction.Tag = single;
            BtnMainAction.Visibility = Visibility.Visible;
        }
        else if (activeSessions.Count > 1)
        {
            TxtMainAction.Text = AppServices.Localization.GetString("Triage.SelectCustomer") ?? "Move to Customer...";
            BtnMainAction.Tag = null;
            BtnMainAction.Visibility = Visibility.Visible;

            var flyout = new MenuFlyout();
            foreach (var session in activeSessions)
            {
                var s = session;
                var item = new MenuFlyoutItem
                {
                    Text = $"{s.Customer.Name} ({s.Customer.Code})",
                    Icon = new FontIcon { Glyph = "\uE77B" }
                };
                item.Click += async (sender, args) => await RouteToCustomerAsync(s);
                flyout.Items.Add(item);
            }
            BtnMainAction.Flyout = flyout;
        }
        else
        {
            BtnMainAction.Flyout = null;
            TxtMainAction.Text = AppServices.Localization.GetString("Triage.CreateSession", "Create a new session?");
            BtnMainAction.Tag = "new_session";
            BtnMainAction.Visibility = Visibility.Visible;
        }

        PositionWidget();
        IsWidgetVisible = true;
        AppWindow.Show();
        _countdownTimer.Start();
    }

    public void HideWidget()
    {
        _countdownTimer.Stop();
        IsWidgetVisible = false;
        _currentFile = null;
        AppWindow.Hide();
    }

    private void PositionWidget()
    {
        var hwnd = Win32Interop.GetWindowFromWindowId(AppWindow.Id);
        var dpi = GetDpiForWindow(hwnd);
        var scale = (dpi == 0 ? 96.0 : dpi) / 96.0;

        int widthDip = 360;
        int heightDip = 145;
        int widthPx = (int)(widthDip * scale);
        int heightPx = (int)(heightDip * scale);

        int screenW = GetSystemMetrics(0); // SM_CXSCREEN
        int screenH = GetSystemMetrics(1); // SM_CYSCREEN
        int margin = (int)(16 * scale);

        // Read user's chosen corner: 0 = BottomRight, 1 = TopRight, 2 = BottomLeft, 3 = TopLeft
        var posIndex = SettingsViewModel.OverlayPositionSetting;

        int posX;
        int posY;

        switch (posIndex)
        {
            case 1: // Top-Right
                posX = screenW - widthPx - margin;
                posY = margin + (int)(32 * scale);
                break;

            case 2: // Bottom-Left
                posX = margin;
                posY = screenH - heightPx - (int)(64 * scale);
                break;

            case 3: // Top-Left
                posX = margin;
                posY = margin + (int)(32 * scale);
                break;

            case 0: // Bottom-Right (Default)
            default:
                // Query taskbar to avoid overlapping
                var abd = new APPBARDATA { cbSize = (uint)Marshal.SizeOf<APPBARDATA>() };
                var tbResult = SHAppBarMessage(ABM_GETTASKBARPOS, ref abd);
                if (tbResult != IntPtr.Zero && abd.rc.Top > 0)
                {
                    posX = screenW - widthPx - margin;
                    posY = abd.rc.Top - heightPx - (int)(8 * scale);
                }
                else
                {
                    posX = screenW - widthPx - margin;
                    posY = screenH - heightPx - (int)(64 * scale);
                }
                break;
        }

        AppWindow.MoveAndResize(new RectInt32(posX, posY, widthPx, heightPx));
    }

    private void CountdownTimer_Tick(object? sender, object e)
    {
        _remainingSeconds -= 0.1;
        if (_remainingSeconds <= 0)
        {
            HideWidget();
            return;
        }

        TimeoutProgressBar.Value = (_remainingSeconds / TotalSeconds) * 100.0;
    }

    private async void MainAction_Click(object sender, RoutedEventArgs e)
    {
        if (BtnMainAction.Tag is ActiveSessionItem session)
        {
            await RouteToCustomerAsync(session);
        }
        else if (BtnMainAction.Tag is string tag && tag == "documents")
        {
            HideWidget();
            MainWindow.Instance?.RestoreWindow();
            MainWindow.Instance?.NavigateTo(typeof(DocumentsPage));
        }
        else if (BtnMainAction.Tag is string tag2 && tag2 == "new_session")
        {
            HideWidget();
            var newSessionWidget = new NewSessionWidget(_currentFile);
            newSessionWidget.Activate();
        }
    }

    private async Task RouteToCustomerAsync(ActiveSessionItem session)
    {
        if (_currentFile == null) return;
        _countdownTimer.Stop();

        var file = _currentFile;
        var success = await AppServices.FileWatcher.RouteFileToCustomerAsync(file.FilePath, session.FolderPath, deleteSource: true);

        if (success)
        {
            TxtMainAction.Text = AppServices.Localization.GetString("Common.Success") ?? "Moved!";
            BtnMainAction.IsEnabled = false;

            // Notify MainWindow and open pages
            MainWindow.Instance?.NotifyIncomingFileRouted();

            await Task.Delay(1200);
            BtnMainAction.IsEnabled = true;
            HideWidget();
        }
        else
        {
            HideWidget();
        }
    }

    private void Dismiss_Click(object sender, RoutedEventArgs e)
    {
        HideWidget();
    }
}
