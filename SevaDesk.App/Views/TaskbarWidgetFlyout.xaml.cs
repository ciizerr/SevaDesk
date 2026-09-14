using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using Windows.Graphics;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using SevaDesk.Core.Models;
using SevaDesk_App.Services;
using Windows.ApplicationModel.DataTransfer;

namespace SevaDesk_App.Views;

public sealed partial class TaskbarWidgetFlyout : Window
{
    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

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

    [DllImport("shell32.dll")]
    private static extern IntPtr SHAppBarMessage(uint dwMessage, ref APPBARDATA pData);

    public bool IsFlyoutVisible { get; private set; }
    private readonly ObservableCollection<ActiveSessionItem> _sessionItems = [];

    public TaskbarWidgetFlyout()
    {
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SessionsListView.ItemsSource = _sessionItems;

        // Make window behave as a toolwindow (excluded from Alt+Tab)
        var hwnd = Win32Interop.GetWindowFromWindowId(AppWindow.Id);
        var exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_TOOLWINDOW);

        // Remove titlebar and standard chrome
        if (AppWindowTitleBar.IsCustomizationSupported())
        {
            AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Collapsed;
        }

        Activated += TaskbarWidgetFlyout_Activated;
    }

    private void TaskbarWidgetFlyout_Activated(object sender, WindowActivatedEventArgs args)
    {
        if (args.WindowActivationState == WindowActivationState.Deactivated)
        {
            HideFlyout();
        }
    }

    public async void ShowFlyout()
    {
        PositionFlyoutAboveTaskbar();
        IsFlyoutVisible = true;
        AppWindow.Show();
        Activate();

        await LoadSessionsAsync();
    }

    public void HideFlyout()
    {
        IsFlyoutVisible = false;
        AppWindow.Hide();
    }

    private void PositionFlyoutAboveTaskbar()
    {
        var hwnd = Win32Interop.GetWindowFromWindowId(AppWindow.Id);
        var dpi = GetDpiForWindow(hwnd);
        var scale = (dpi == 0 ? 96.0 : dpi) / 96.0;

        int widthDip = 380;
        int heightDip = 520;
        int widthPx = (int)(widthDip * scale);
        int heightPx = (int)(heightDip * scale);

        // Query taskbar position via Win32 Shell API
        var abd = new APPBARDATA
        {
            cbSize = (uint)Marshal.SizeOf<APPBARDATA>()
        };

        var result = SHAppBarMessage(ABM_GETTASKBARPOS, ref abd);
        int posX;
        int posY;

        if (result != IntPtr.Zero)
        {
            // Position above the taskbar in the bottom-right corner
            int taskbarTop = abd.rc.Top;
            int taskbarRight = abd.rc.Right;
            int margin = (int)(12 * scale);

            posX = taskbarRight - widthPx - margin;
            posY = taskbarTop - heightPx - margin;

            // Ensure window stays within screen bounds
            if (posY < 0) posY = (int)(20 * scale);
            if (posX < 0) posX = (int)(20 * scale);
        }
        else
        {
            // Fallback: bottom-right corner of primary monitor
            int screenW = GetSystemMetrics(0); // SM_CXSCREEN
            int screenH = GetSystemMetrics(1); // SM_CYSCREEN
            posX = screenW - widthPx - (int)(16 * scale);
            posY = screenH - heightPx - (int)(56 * scale);
        }

        AppWindow.MoveAndResize(new RectInt32(posX, posY, widthPx, heightPx));
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
            EmptyStatePanel.Visibility = count == 0 ? Visibility.Visible : Visibility.Collapsed;
            SessionsListView.Visibility = count > 0 ? Visibility.Visible : Visibility.Collapsed;
        });
    }

    private void OpenFullApp_Click(object sender, RoutedEventArgs e)
    {
        HideFlyout();
        MainWindow.Instance?.RestoreWindow();
    }

    private void CloseFlyout_Click(object sender, RoutedEventArgs e)
    {
        HideFlyout();
    }

    private async void EndSessionItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string sessionId)
        {
            await AppServices.Sessions.CompleteSessionAsync(sessionId);
            await LoadSessionsAsync();
            await TaskbarWidgetService.Instance.RefreshLiveSessionsAsync();
        }
    }

    private async void QuickStart_Click(object sender, RoutedEventArgs e)
    {
        await ExecuteQuickStartAsync();
    }

    private async void TxtQuickCustomerName_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            await ExecuteQuickStartAsync();
        }
    }

    private async Task ExecuteQuickStartAsync()
    {
        var name = TxtQuickCustomerName.Text?.Trim();
        if (string.IsNullOrWhiteSpace(name)) return;

        TxtQuickCustomerName.Text = string.Empty;

        var customer = await AppServices.Customers.CreateAsync(new Customer
        {
            Name = name
        });

        var session = await AppServices.Sessions.StartSessionAsync(customer.Id);
        var folder = AppServices.FolderManager.EnsureCustomerWorkingFolder(customer.Name, customer.Code);
        session.FolderPath = folder;
        session.Customer = customer;
        session.FolderStats = new FolderStats();

        await LoadSessionsAsync();
        await TaskbarWidgetService.Instance.RefreshLiveSessionsAsync();
    }

    private void ToggleUpiCard_Click(object sender, RoutedEventArgs e)
    {
        UpiCardPanel.Visibility = UpiCardPanel.Visibility == Visibility.Visible
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    private void CopyUpi_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dataPackage = new DataPackage();
            dataPackage.SetText(TxtUpiVpa.Text);
            Clipboard.SetContent(dataPackage);
            TaskbarWidgetService.Instance.ShowNotification("UPI ID Copied", $"{TxtUpiVpa.Text} copied to clipboard.");
        }
        catch { }
    }
}
