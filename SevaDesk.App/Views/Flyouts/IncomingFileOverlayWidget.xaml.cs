using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Windows.Graphics;
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
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
    private readonly DispatcherTimer _undoTimer = new();
    private double _remainingSeconds = 12.0;
    private const double TotalSeconds = 12.0;
    private double _undoRemainingSeconds = 5.0;
    private const double TotalUndoSeconds = 5.0;
    private bool _isPointerOver = false;

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

        _undoTimer.Interval = TimeSpan.FromMilliseconds(100);
        _undoTimer.Tick += UndoTimer_Tick;
    }

    public async void ShowForFile(IncomingFileItem file)
    {
        _currentFile = file;
        _undoTimer.Stop();
        _remainingSeconds = TotalSeconds;
        TimeoutProgressBar.Value = 100;

        // Switch to Triage Mode
        TriageContentPanel.Visibility = Visibility.Visible;
        AutoMovedUndoPanel.Visibility = Visibility.Collapsed;

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

        // Reset auto route checkbox
        ChkAutoRoute.IsChecked = false;

        // Fetch active sessions and build dynamic 1-click routing buttons
        var activeSessions = (await AppServices.Sessions.GetActiveSessionsAsync()).ToList();
        int heightDip = 175;

        ActionButtonsGrid.Children.Clear();
        ActionButtonsGrid.RowDefinitions.Clear();
        ActionButtonsGrid.ColumnDefinitions.Clear();

        if (activeSessions.Count == 1)
        {
            ChkAutoRoute.Visibility = Visibility.Visible;
            var session = activeSessions[0];

            var btn = new Button
            {
                Style = (Style)Application.Current.Resources["AccentButtonStyle"],
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Height = 34,
                Content = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Children =
                    {
                        new FontIcon { Glyph = "\uE8B7", FontSize = 12 },
                        new TextBlock
                        {
                            Text = string.Format(AppServices.Localization.GetString("Triage.MoveToSingle") ?? "Move to {0}", session.Customer.Name),
                            FontWeight = FontWeights.SemiBold,
                            FontSize = 12,
                            TextTrimming = TextTrimming.CharacterEllipsis
                        }
                    }
                }
            };
            btn.Click += async (s, e) => await RouteToCustomerSessionAsync(session);
            ActionButtonsGrid.Children.Add(btn);
        }
        else if (activeSessions.Count == 2)
        {
            ChkAutoRoute.Visibility = Visibility.Visible;

            ActionButtonsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            ActionButtonsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8, GridUnitType.Pixel) });
            ActionButtonsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            for (int i = 0; i < 2; i++)
            {
                var session = activeSessions[i];
                var btn = new Button
                {
                    Style = (Style)Application.Current.Resources["AccentButtonStyle"],
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Height = 34,
                    Content = new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = 6,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Children =
                        {
                            new FontIcon { Glyph = "\uE8B7", FontSize = 12 },
                            new TextBlock
                            {
                                Text = session.Customer.Name,
                                FontWeight = FontWeights.SemiBold,
                                FontSize = 12,
                                TextTrimming = TextTrimming.CharacterEllipsis
                            }
                        }
                    }
                };
                ToolTipService.SetToolTip(btn, $"Move to {session.Customer.Name} ({session.Customer.Code})");
                btn.Click += async (s, e) => await RouteToCustomerSessionAsync(session);
                Grid.SetColumn(btn, i * 2);
                ActionButtonsGrid.Children.Add(btn);
            }
        }
        else if (activeSessions.Count is >= 3 and <= 4)
        {
            heightDip = 205;
            ChkAutoRoute.Visibility = Visibility.Visible;

            ActionButtonsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            ActionButtonsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8, GridUnitType.Pixel) });
            ActionButtonsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            ActionButtonsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            ActionButtonsGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(6, GridUnitType.Pixel) });
            ActionButtonsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            for (int i = 0; i < activeSessions.Count; i++)
            {
                var session = activeSessions[i];
                var btn = new Button
                {
                    Style = (Style)Application.Current.Resources["AccentButtonStyle"],
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Height = 30,
                    Content = new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = 6,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Children =
                        {
                            new FontIcon { Glyph = "\uE8B7", FontSize = 11 },
                            new TextBlock
                            {
                                Text = session.Customer.Name,
                                FontWeight = FontWeights.SemiBold,
                                FontSize = 11,
                                TextTrimming = TextTrimming.CharacterEllipsis
                            }
                        }
                    }
                };
                ToolTipService.SetToolTip(btn, $"Move to {session.Customer.Name} ({session.Customer.Code})");
                btn.Click += async (s, e) => await RouteToCustomerSessionAsync(session);

                int col = (i % 2) * 2;
                int row = (i / 2) * 2;
                Grid.SetColumn(btn, col);
                Grid.SetRow(btn, row);
                ActionButtonsGrid.Children.Add(btn);
            }
        }
        else if (activeSessions.Count > 4)
        {
            ChkAutoRoute.Visibility = Visibility.Visible;

            ActionButtonsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            ActionButtonsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8, GridUnitType.Pixel) });
            ActionButtonsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            ActionButtonsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8, GridUnitType.Pixel) });
            ActionButtonsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // Button 1
            var s1 = activeSessions[0];
            var btn1 = CreateSessionButton(s1);
            Grid.SetColumn(btn1, 0);
            ActionButtonsGrid.Children.Add(btn1);

            // Button 2
            var s2 = activeSessions[1];
            var btn2 = CreateSessionButton(s2);
            Grid.SetColumn(btn2, 2);
            ActionButtonsGrid.Children.Add(btn2);

            // Button 3: More flyout
            var moreBtn = new Button
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Height = 34,
                Content = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 4,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Children =
                    {
                        new TextBlock { Text = $"More ({activeSessions.Count - 2})", FontSize = 11 },
                        new FontIcon { Glyph = "\uE70D", FontSize = 10 }
                    }
                }
            };

            var flyout = new MenuFlyout();
            for (int i = 2; i < activeSessions.Count; i++)
            {
                var s = activeSessions[i];
                var item = new MenuFlyoutItem
                {
                    Text = $"{s.Customer.Name} ({s.Customer.Code})",
                    Icon = new FontIcon { Glyph = "\uE77B" }
                };
                item.Click += async (sender, args) => await RouteToCustomerSessionAsync(s);
                flyout.Items.Add(item);
            }
            moreBtn.Flyout = flyout;
            Grid.SetColumn(moreBtn, 4);
            ActionButtonsGrid.Children.Add(moreBtn);
        }
        else
        {
            // 0 active sessions
            ChkAutoRoute.Visibility = Visibility.Collapsed;

            ActionButtonsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            ActionButtonsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8, GridUnitType.Pixel) });
            ActionButtonsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var newSessionBtn = new Button
            {
                Style = (Style)Application.Current.Resources["AccentButtonStyle"],
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Height = 34,
                Content = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 6,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Children =
                    {
                        new FontIcon { Glyph = "\uE710", FontSize = 12 },
                        new TextBlock { Text = "New Session", FontWeight = FontWeights.SemiBold, FontSize = 12 }
                    }
                }
            };
            newSessionBtn.Click += (s, e) =>
            {
                HideWidget();
                var newSessionWidget = new NewSessionWidget(_currentFile);
                newSessionWidget.Activate();
            };
            Grid.SetColumn(newSessionBtn, 0);
            ActionButtonsGrid.Children.Add(newSessionBtn);

            var dismissBtn = new Button
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Height = 34,
                Content = new TextBlock { Text = "Dismiss", FontSize = 12 }
            };
            dismissBtn.Click += (s, e) => HideWidget();
            Grid.SetColumn(dismissBtn, 2);
            ActionButtonsGrid.Children.Add(dismissBtn);
        }

        PositionWidget(heightDip);
        IsWidgetVisible = true;
        AppWindow.Show();
        _countdownTimer.Start();
    }

    private Button CreateSessionButton(ActiveSessionItem session)
    {
        var btn = new Button
        {
            Style = (Style)Application.Current.Resources["AccentButtonStyle"],
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Height = 34,
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 6,
                HorizontalAlignment = HorizontalAlignment.Center,
                Children =
                {
                    new FontIcon { Glyph = "\uE8B7", FontSize = 12 },
                    new TextBlock
                    {
                        Text = session.Customer.Name,
                        FontWeight = FontWeights.SemiBold,
                        FontSize = 11,
                        TextTrimming = TextTrimming.CharacterEllipsis
                    }
                }
            }
        };
        ToolTipService.SetToolTip(btn, $"Move to {session.Customer.Name} ({session.Customer.Code})");
        btn.Click += async (s, e) => await RouteToCustomerSessionAsync(session);
        return btn;
    }

    private async Task RouteToCustomerSessionAsync(ActiveSessionItem session)
    {
        if (_currentFile == null) return;
        _countdownTimer.Stop();

        var file = _currentFile;
        bool isAutoRouteChecked = ChkAutoRoute.IsChecked == true;
        if (isAutoRouteChecked)
        {
            AppServices.FileWatcher.SetAutoRoute(session.Session.Id, session.Customer.Name);
        }

        var (success, _) = await AppServices.FileWatcher.RouteFileToCustomerExAsync(file.FilePath, session.FolderPath, deleteSource: true);

        if (success)
        {
            MainWindow.Instance?.NotifyIncomingFileRouted();

            if (isAutoRouteChecked)
            {
                ToastService.Instance.ShowSuccess($"Auto-move enabled for {session.Customer.Name}. Subsequent files will route automatically.");
            }

            HideWidget();
        }
        else
        {
            HideWidget();
        }
    }

    public void ShowAutoMovedToast(string fileName, string customerName, string destPath, string sourcePath)
    {
        _countdownTimer.Stop();
        _undoRemainingSeconds = TotalUndoSeconds;
        UndoTimeoutProgressBar.Value = 100;

        // Switch to Undo Mode
        TriageContentPanel.Visibility = Visibility.Collapsed;
        AutoMovedUndoPanel.Visibility = Visibility.Visible;

        TxtAutoMovedFileName.Text = fileName;
        TxtAutoMovedTarget.Text = $"Moved to {customerName}'s folder";

        PositionWidget(165);
        IsWidgetVisible = true;
        AppWindow.Show();
        _undoTimer.Start();
    }

    public void HideWidget()
    {
        _countdownTimer.Stop();
        _undoTimer.Stop();
        IsWidgetVisible = false;
        _currentFile = null;
        _isPointerOver = false;
        AppWindow.Hide();
    }

    private void PositionWidget(int heightDip = 175)
    {
        var hwnd = Win32Interop.GetWindowFromWindowId(AppWindow.Id);
        var dpi = GetDpiForWindow(hwnd);
        var scale = (dpi == 0 ? 96.0 : dpi) / 96.0;

        int widthDip = 400;
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
        if (_isPointerOver) return;

        _remainingSeconds -= 0.1;
        if (_remainingSeconds <= 0)
        {
            HideWidget();
            return;
        }

        TimeoutProgressBar.Value = (_remainingSeconds / TotalSeconds) * 100.0;
    }

    private void UndoTimer_Tick(object? sender, object e)
    {
        if (_isPointerOver) return;

        _undoRemainingSeconds -= 0.1;
        if (_undoRemainingSeconds <= 0)
        {
            HideWidget();
            return;
        }

        UndoTimeoutProgressBar.Value = (_undoRemainingSeconds / TotalUndoSeconds) * 100.0;
    }

    private async void UndoAutoMove_Click(object sender, RoutedEventArgs e)
    {
        _undoTimer.Stop();

        var (success, restoredPath) = await AppServices.FileWatcher.UndoLastAutoMoveAsync();
        if (success)
        {
            MainWindow.Instance?.NotifyIncomingFileRouted();
            var dirName = Path.GetFileName(Path.GetDirectoryName(restoredPath) ?? "original folder");
            ToastService.Instance.ShowSuccess($"Restored to {dirName}. Auto-move disabled.");
        }
        else
        {
            ToastService.Instance.ShowError("Could not undo file move.");
        }

        HideWidget();
    }

    private void Widget_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        _isPointerOver = true;
    }

    private void Widget_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        _isPointerOver = false;
    }

    private void Dismiss_Click(object sender, RoutedEventArgs e)
    {
        HideWidget();
    }
}
