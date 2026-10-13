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
using Microsoft.UI.Xaml.Media;
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
    
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWCP_ROUND = 2;
    private const int DWMWA_BORDER_COLOR = 34;
    private const int DWMWA_COLOR_NONE = unchecked((int)0xFFFFFFFE);

    private static void ApplyDwmWindowAttributes(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return;

        // 1. Force Immersive Dark Mode on the HWND. Without this, Windows 11 treats the HWND
        // as a light-theme window and renders a default light-grey/white 1px border around the entire window perimeter.
        int darkMode = 1;
        DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkMode, sizeof(int));

        // 2. Suppress the DWM window border color completely (DWMWA_COLOR_NONE = 0xFFFFFFFE).
        int colorNone = DWMWA_COLOR_NONE;
        DwmSetWindowAttribute(hwnd, DWMWA_BORDER_COLOR, ref colorNone, sizeof(int));

        // 3. Set native Windows 11 rounded corners.
        int cornerPref = DWMWCP_ROUND;
        DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref cornerPref, sizeof(int));
    }

    private const int GWL_STYLE = -16;
    private const int WS_BORDER = 0x00800000;
    private const int WS_DLGFRAME = 0x00400000;
    private const int WS_CAPTION = 0x00C00000;
    private const int WS_THICKFRAME = 0x00040000;
    private const int WS_POPUP = unchecked((int)0x80000000);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_FRAMECHANGED = 0x0020;

    private static void RemoveWindowFrameStyles(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return;

        int style = GetWindowLong(hwnd, GWL_STYLE);
        style &= ~WS_BORDER & ~WS_DLGFRAME & ~WS_CAPTION & ~WS_THICKFRAME;
        SetWindowLong(hwnd, GWL_STYLE, style);

        int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        exStyle &= ~0x00000100; // WS_EX_WINDOWEDGE
        exStyle &= ~0x00000200; // WS_EX_CLIENTEDGE
        exStyle &= ~0x00000001; // WS_EX_DLGMODALFRAME
        SetWindowLong(hwnd, GWL_EXSTYLE, exStyle);

        SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_FRAMECHANGED);
    }



    [DllImport("shell32.dll")]
    private static extern IntPtr SHAppBarMessage(uint dwMessage, ref APPBARDATA pData);

    private static IncomingFileOverlayWidget? _instance;
    public static IncomingFileOverlayWidget Instance => _instance ??= new IncomingFileOverlayWidget();

    private IncomingFileItem? _currentFile;
    private readonly DispatcherTimer _countdownTimer = new();
    private readonly DispatcherTimer _undoTimer = new();
    private double _remainingSeconds = 12.0;
    private const double TotalSeconds = 12.0;
    private double _undoRemainingSeconds = 8.0;
    private const double TotalUndoSeconds = 8.0;
    private bool _isPointerOver = false;

    private string? _currentAutoMovedDestPath;
    private string? _currentAutoMovedCustomerName;
    private ActiveSessionItem? _currentAutoMovedSession;

    private string? _selectedTag = null;
    private Button? _selectedChipBtn = null;
    private List<ActiveSessionItem> _activeSessions = [];
    private bool _suppressChipClick = false;

    public bool IsWidgetVisible { get; private set; }
    public IncomingFileItem? CurrentFile => _currentFile;

    public IncomingFileOverlayWidget()
    {
        InitializeComponent();

        var hwnd = Win32Interop.GetWindowFromWindowId(AppWindow.Id);
        var exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_TOOLWINDOW);

        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsAlwaysOnTop = true;
            presenter.IsResizable = false;
            presenter.SetBorderAndTitleBar(false, false);
        }

        RemoveWindowFrameStyles(hwnd);
        ApplyDwmWindowAttributes(hwnd);

        Activated += (s, e) =>
        {
            RemoveWindowFrameStyles(hwnd);
            ApplyDwmWindowAttributes(hwnd);
        };

        _countdownTimer.Interval = TimeSpan.FromMilliseconds(100);
        _countdownTimer.Tick += CountdownTimer_Tick;

        _undoTimer.Interval = TimeSpan.FromMilliseconds(100);
        _undoTimer.Tick += UndoTimer_Tick;
    }

    public async void ShowForFile(IncomingFileItem file)
    {
        _currentFile = file;
        _selectedTag = null;
        _selectedChipBtn = null;
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

        // Fetch active sessions
        _activeSessions = (await AppServices.Sessions.GetActiveSessionsAsync()).ToList();

        // Populate Quick Rename Chips
        PopulateDocChips();

        // Refresh Action Buttons
        RefreshActionButtons();

        int heightDip = _activeSessions.Count switch
        {
            0 => 175,
            1 => 265,
            2 => 265,
            3 or 4 => 315,
            _ => 270
        };

        PositionWidget(heightDip);
        IsWidgetVisible = true;
        var hwnd = Win32Interop.GetWindowFromWindowId(AppWindow.Id);
        RemoveWindowFrameStyles(hwnd);
        AppWindow.Show();
        RemoveWindowFrameStyles(hwnd);
        ApplyDwmWindowAttributes(hwnd);
        _countdownTimer.Start();
    }

    private void PopulateDocChips()
    {
        DocChipsPanel.Children.Clear();

        if (_activeSessions.Count == 0)
        {
            QuickRenamePanel.Visibility = Visibility.Collapsed;
            return;
        }

        QuickRenamePanel.Visibility = Visibility.Visible;

        var tags = SmartTagHelper.GetDocumentTagsForSessions(_activeSessions);

        if (_activeSessions.Count == 1)
        {
            TxtRenamePrompt.Text = $"Quick Tag & Move to {_activeSessions[0].Customer.Name}:";
        }
        else
        {
            TxtRenamePrompt.Text = "Select Tag & Move to Customer:";
        }

        // 1. Unused tags first (standard & custom)
        foreach (var tag in tags.Where(t => !t.IsUsed))
        {
            var chip = CreateDocChipButton(tag);
            DocChipsPanel.Children.Add(chip);
        }

        // 2. [+ Custom...] chip
        var customChip = CreateCustomChipButton();
        DocChipsPanel.Children.Add(customChip);

        // 3. Already-existing tags at the very end
        foreach (var tag in tags.Where(t => t.IsUsed))
        {
            var chip = CreateDocChipButton(tag);
            DocChipsPanel.Children.Add(chip);
        }
    }

    private Button CreateDocChipButton(DocumentTagItem tag)
    {
        var chip = new Button
        {
            Height = 28,
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(8, 2, tag.IsCustom ? 4 : 8, 2),
            Tag = tag.TagKey
        };

        var sp = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5, VerticalAlignment = VerticalAlignment.Center };
        var icon = new FontIcon { FontSize = 10 };
        var text = new TextBlock { FontSize = 11 };

        if (tag.IsUsed)
        {
            icon.Glyph = "\uE73E"; // Checkmark
            icon.Foreground = (Brush)Application.Current.Resources["SystemFillColorSuccessBrush"];
            text.Text = $"✓ {tag.DisplayName}";
            chip.Opacity = 0.6;
            string tipTarget = _activeSessions.Count == 1
                ? _activeSessions[0].Customer.Name
                : "all active customers";
            ToolTipService.SetToolTip(chip, $"Already collected for {tipTarget}. Click to save as an additional copy ({tag.TagKey}_2).");
        }
        else
        {
            icon.Glyph = tag.Glyph;
            text.Text = tag.DisplayName;
            ToolTipService.SetToolTip(chip, $"Rename to '{tag.TagKey}' and move");
        }

        sp.Children.Add(icon);
        sp.Children.Add(text);

        if (tag.IsCustom)
        {
            var delBtn = new Button
            {
                Width = 18,
                Height = 18,
                Padding = new Thickness(0),
                CornerRadius = new CornerRadius(9),
                Background = new SolidColorBrush(Colors.Transparent),
                BorderThickness = new Thickness(0),
                Margin = new Thickness(4, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Content = new FontIcon { Glyph = "\uE711", FontSize = 8, Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"] }
            };
            ToolTipService.SetToolTip(delBtn, $"Delete saved tag '{tag.DisplayName}'");
            delBtn.Click += async (s, e) =>
            {
                _suppressChipClick = true;
                try
                {
                    await AppServices.CustomTags.DeleteAsync(tag.TagKey);
                    DocChipsPanel.Children.Remove(chip);
                    ToastService.Instance.ShowInfo($"Removed custom tag '{tag.DisplayName}'");
                }
                finally
                {
                    await Task.Delay(150);
                    _suppressChipClick = false;
                }
            };
            sp.Children.Add(delBtn);
        }

        chip.Content = sp;

        chip.Click += async (s, e) =>
        {
            if (_suppressChipClick) return;

            if (_activeSessions.Count == 1)
            {
                // 1-Click: Rename & Move immediately!
                await RouteWithTagAsync(_activeSessions[0], tag.TagKey);
            }
            else
            {
                // Multi-session: Toggle selected tag and update customer action buttons
                if (_selectedTag == tag.TagKey)
                {
                    _selectedTag = null;
                    _selectedChipBtn = null;
                    chip.ClearValue(Button.BackgroundProperty);
                    chip.ClearValue(Button.BorderBrushProperty);
                }
                else
                {
                    if (_selectedChipBtn != null)
                    {
                        _selectedChipBtn.ClearValue(Button.BackgroundProperty);
                        _selectedChipBtn.ClearValue(Button.BorderBrushProperty);
                    }
                    _selectedTag = tag.TagKey;
                    _selectedChipBtn = chip;
                    chip.Background = (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"];
                    chip.Foreground = new SolidColorBrush(Colors.White);
                    icon.Foreground = new SolidColorBrush(Colors.White);
                }
                RefreshActionButtons();
            }
        };

        return chip;
    }

    private Button CreateCustomChipButton()
    {
        var customBtn = new Button
        {
            Height = 28,
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(8, 2, 8, 2)
        };

        var sp = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
        sp.Children.Add(new FontIcon { Glyph = "\uE710", FontSize = 10 });
        sp.Children.Add(new TextBlock { Text = "Custom...", FontSize = 11 });
        customBtn.Content = sp;
        ToolTipService.SetToolTip(customBtn, "Enter custom document name (saved automatically)");

        var flyout = new Flyout();
        var flyoutStack = new StackPanel { Width = 220, Spacing = 8 };
        flyoutStack.Children.Add(new TextBlock { Text = "Custom Document Name", FontWeight = FontWeights.SemiBold, FontSize = 12 });

        var tb = new TextBox { PlaceholderText = "e.g. voter_id, affidavit", FontSize = 12 };
        flyoutStack.Children.Add(tb);

        var applyBtn = new Button
        {
            Content = _activeSessions.Count == 1 ? "Rename & Move" : "Select Name",
            Style = (Style)Application.Current.Resources["AccentButtonStyle"],
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        applyBtn.Click += async (s, e) =>
        {
            var raw = tb.Text?.Trim();
            if (string.IsNullOrWhiteSpace(raw)) return;
            var saved = await SmartTagHelper.SaveCustomTagAsync(raw);
            var cleanTag = saved?.TagKey ?? SmartTagHelper.NormalizeTagKey(raw);
            flyout.Hide();

            if (_activeSessions.Count == 1)
            {
                await RouteWithTagAsync(_activeSessions[0], cleanTag);
            }
            else
            {
                _selectedTag = cleanTag;
                RefreshActionButtons();
            }
        };

        flyoutStack.Children.Add(applyBtn);
        flyout.Content = flyoutStack;
        customBtn.Flyout = flyout;

        return customBtn;
    }

    private void RefreshActionButtons()
    {
        ActionButtonsGrid.Children.Clear();
        ActionButtonsGrid.RowDefinitions.Clear();
        ActionButtonsGrid.ColumnDefinitions.Clear();

        if (_activeSessions.Count == 1)
        {
            ChkAutoRoute.Visibility = Visibility.Visible;
            var session = _activeSessions[0];
            BtnHeaderWalkIn.Visibility = session.Customer.Id != "walk-in" ? Visibility.Visible : Visibility.Collapsed;

            string btnText = _selectedTag != null
                ? $"Move to {session.Customer.Name} ({_selectedTag})"
                : $"Move to {session.Customer.Name}";

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
                            Text = btnText,
                            FontWeight = FontWeights.SemiBold,
                            FontSize = 12,
                            TextTrimming = TextTrimming.CharacterEllipsis
                        }
                    }
                }
            };
            ToolTipService.SetToolTip(btn, $"Move file to {session.Customer.Name} ({session.Customer.Code})");
            btn.Click += async (s, e) => await RouteToCustomerSessionAsync(session);

            ActionButtonsGrid.Children.Add(btn);
        }
        else if (_activeSessions.Count == 2)
        {
            ChkAutoRoute.Visibility = Visibility.Visible;
            BtnHeaderWalkIn.Visibility = Visibility.Visible;

            ActionButtonsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            ActionButtonsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8, GridUnitType.Pixel) });
            ActionButtonsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            for (int i = 0; i < 2; i++)
            {
                var session = _activeSessions[i];
                string label = _selectedTag != null
                    ? $"{session.Customer.Name} ({_selectedTag})"
                    : session.Customer.Name;

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
                                Text = label,
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
        else if (_activeSessions.Count is >= 3 and <= 4)
        {
            ChkAutoRoute.Visibility = Visibility.Visible;
            BtnHeaderWalkIn.Visibility = Visibility.Visible;

            ActionButtonsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            ActionButtonsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8, GridUnitType.Pixel) });
            ActionButtonsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            ActionButtonsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            ActionButtonsGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(6, GridUnitType.Pixel) });
            ActionButtonsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            for (int i = 0; i < _activeSessions.Count; i++)
            {
                var session = _activeSessions[i];
                string label = _selectedTag != null
                    ? $"{session.Customer.Name} ({_selectedTag})"
                    : session.Customer.Name;

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
                                Text = label,
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
        else if (_activeSessions.Count > 4)
        {
            ChkAutoRoute.Visibility = Visibility.Visible;
            BtnHeaderWalkIn.Visibility = Visibility.Visible;

            ActionButtonsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            ActionButtonsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8, GridUnitType.Pixel) });
            ActionButtonsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            ActionButtonsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8, GridUnitType.Pixel) });
            ActionButtonsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var s1 = _activeSessions[0];
            var btn1 = CreateSessionButton(s1);
            Grid.SetColumn(btn1, 0);
            ActionButtonsGrid.Children.Add(btn1);

            var s2 = _activeSessions[1];
            var btn2 = CreateSessionButton(s2);
            Grid.SetColumn(btn2, 2);
            ActionButtonsGrid.Children.Add(btn2);

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
                        new TextBlock { Text = $"More ({_activeSessions.Count - 2})", FontSize = 11 },
                        new FontIcon { Glyph = "\uE70D", FontSize = 10 }
                    }
                }
            };

            var flyout = new MenuFlyout();
            for (int i = 2; i < _activeSessions.Count; i++)
            {
                var s = _activeSessions[i];
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
            BtnHeaderWalkIn.Visibility = Visibility.Collapsed;

            ActionButtonsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            ActionButtonsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8, GridUnitType.Pixel) });
            ActionButtonsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var walkInBtn = new Button
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
                        new FontIcon { Glyph = "\uE945", FontSize = 12 },
                        new TextBlock { Text = "Quick Walk-in", FontWeight = FontWeights.SemiBold, FontSize = 12 }
                    }
                }
            };
            ToolTipService.SetToolTip(walkInBtn, "1-Click Move to today's Walk-in temp folder & open for printing");
            walkInBtn.Click += async (s, e) => await RouteToQuickWalkInAsync();
            Grid.SetColumn(walkInBtn, 0);
            ActionButtonsGrid.Children.Add(walkInBtn);

            var newSessionBtn = new Button
            {
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
                        new TextBlock { Text = "New Customer", FontWeight = FontWeights.SemiBold, FontSize = 12 }
                    }
                }
            };
            newSessionBtn.Click += (s, e) =>
            {
                var fileToPass = _currentFile;
                HideWidget();
                if (fileToPass != null)
                {
                    var newSessionWidget = new NewSessionWidget(fileToPass);
                    newSessionWidget.Activate();
                }
            };
            Grid.SetColumn(newSessionBtn, 2);
            ActionButtonsGrid.Children.Add(newSessionBtn);
        }
    }

    private Button CreateSessionButton(ActiveSessionItem session)
    {
        string label = _selectedTag != null
            ? $"{session.Customer.Name} ({_selectedTag})"
            : session.Customer.Name;

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
                        Text = label,
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

    private async Task RouteWithTagAsync(ActiveSessionItem session, string tagKey)
    {
        if (_currentFile == null) return;
        _countdownTimer.Stop();

        var file = _currentFile;
        bool isAutoRouteChecked = ChkAutoRoute.IsChecked == true;
        if (isAutoRouteChecked)
        {
            AppServices.FileWatcher.SetAutoRoute(session.Session.Id, session.Customer.Name);
        }

        var (success, destPath) = await AppServices.FileWatcher.RouteFileWithRenameAndUndoTrackingAsync(
            file.FilePath, session.FolderPath, tagKey, session.Customer.Name, session.Session.Id);

        if (success)
        {
            if (SmartTagHelper.IsPhotoTag(tagKey) && SmartTagHelper.IsImageFile(destPath))
            {
                await SmartTagHelper.TryAssignCustomerPhotoAsync(session.Customer, destPath);
            }

            MainWindow.Instance?.NotifyIncomingFileRouted();
            var renamedName = Path.GetFileName(destPath);

            ToastService.Instance.ShowSuccess($"Moved as '{renamedName}' to {session.Customer.Name}");

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

    private async Task RouteToCustomerSessionAsync(ActiveSessionItem session)
    {
        if (_currentFile == null) return;

        if (!string.IsNullOrWhiteSpace(_selectedTag))
        {
            await RouteWithTagAsync(session, _selectedTag);
            return;
        }

        _countdownTimer.Stop();

        var file = _currentFile;
        bool isAutoRouteChecked = ChkAutoRoute.IsChecked == true;
        if (isAutoRouteChecked)
        {
            AppServices.FileWatcher.SetAutoRoute(session.Session.Id, session.Customer.Name);
        }

        var (success, destPath) = await AppServices.FileWatcher.RouteFileToSessionWithUndoTrackingAsync(
            file.FilePath, session.FolderPath, session.Customer.Name, session.Session.Id);

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

    private async void QuickWalkIn_Click(object sender, RoutedEventArgs e)
    {
        await RouteToQuickWalkInAsync();
    }

    private async Task RouteToQuickWalkInAsync()
    {
        var file = _currentFile;
        if (file == null) return;

        _countdownTimer.Stop();
        HideWidget();

        try
        {
            var walkInSession = await AppServices.Sessions.StartSessionAsync("walk-in");
            var targetFolder = AppServices.FolderManager.EnsureWalkInTempFolderPath();

            var (success, destPath) = await AppServices.FileWatcher.RouteFileToSessionWithUndoTrackingAsync(
                file.FilePath, targetFolder, walkInSession.Customer.Name, walkInSession.Session.Id);

            if (success)
            {
                MainWindow.Instance?.NotifyIncomingFileRouted();
                AppServices.FolderManager.OpenFolderInExplorer(targetFolder);
                ToastService.Instance.ShowSuccess("File moved to today's Walk-in temp folder for printing");
            }
        }
        catch (Exception ex)
        {
            ToastService.Instance.ShowError($"Could not route to Walk-in: {ex.Message}");
        }
    }

    public void ShowAutoMovedToast(string fileName, string customerName, string destPath, string sourcePath, ActiveSessionItem? session = null)
    {
        _countdownTimer.Stop();
        _undoRemainingSeconds = TotalUndoSeconds;
        UndoTimeoutProgressBar.Value = 100;

        _currentAutoMovedDestPath = destPath;
        _currentAutoMovedCustomerName = customerName;
        _currentAutoMovedSession = session;

        // Switch to Undo Mode
        TriageContentPanel.Visibility = Visibility.Collapsed;
        AutoMovedUndoPanel.Visibility = Visibility.Visible;

        TxtAutoMovedFileName.Text = fileName;
        TxtAutoMovedTarget.Text = $"Moved to {customerName}'s folder";
        TxtAutoMovedTarget.ClearValue(TextBlock.ForegroundProperty);

        PopulateAutoMovedChips();

        PositionWidget(230);
        IsWidgetVisible = true;
        var hwnd = Win32Interop.GetWindowFromWindowId(AppWindow.Id);
        RemoveWindowFrameStyles(hwnd);
        AppWindow.Show();
        RemoveWindowFrameStyles(hwnd);
        ApplyDwmWindowAttributes(hwnd);
        _undoTimer.Start();
    }

    private void PopulateAutoMovedChips()
    {
        AutoMovedChipsPanel.Children.Clear();

        var tags = SmartTagHelper.GetDocumentTagsForSession(_currentAutoMovedSession);

        // 1. Unused tags first (standard & custom)
        foreach (var tag in tags.Where(t => !t.IsUsed))
        {
            var chip = CreateAutoMovedDocChipButton(tag);
            AutoMovedChipsPanel.Children.Add(chip);
        }

        // 2. [+ Custom...] chip
        var customChip = CreateAutoMovedCustomChipButton();
        AutoMovedChipsPanel.Children.Add(customChip);

        // 3. Already-existing tags at the very end
        foreach (var tag in tags.Where(t => t.IsUsed))
        {
            var chip = CreateAutoMovedDocChipButton(tag);
            AutoMovedChipsPanel.Children.Add(chip);
        }
    }

    private Button CreateAutoMovedDocChipButton(DocumentTagItem tag)
    {
        var chip = new Button
        {
            Height = 28,
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(8, 2, tag.IsCustom ? 4 : 8, 2),
            Tag = tag.TagKey
        };

        var sp = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5, VerticalAlignment = VerticalAlignment.Center };
        var icon = new FontIcon { FontSize = 10 };
        var text = new TextBlock { FontSize = 11 };

        if (tag.IsUsed)
        {
            icon.Glyph = "\uE73E"; // Checkmark
            icon.Foreground = (Brush)Application.Current.Resources["SystemFillColorSuccessBrush"];
            text.Text = $"✓ {tag.DisplayName}";
            chip.Opacity = 0.6;
            ToolTipService.SetToolTip(chip, $"Already collected for {_currentAutoMovedCustomerName}. Click to rename as an additional copy ({tag.TagKey}_2).");
        }
        else
        {
            icon.Glyph = tag.Glyph;
            text.Text = tag.DisplayName;
            ToolTipService.SetToolTip(chip, $"Rename to '{tag.TagKey}'");
        }

        sp.Children.Add(icon);
        sp.Children.Add(text);

        if (tag.IsCustom)
        {
            var delBtn = new Button
            {
                Width = 18,
                Height = 18,
                Padding = new Thickness(0),
                CornerRadius = new CornerRadius(9),
                Background = new SolidColorBrush(Colors.Transparent),
                BorderThickness = new Thickness(0),
                Margin = new Thickness(4, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Content = new FontIcon { Glyph = "\uE711", FontSize = 8, Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"] }
            };
            ToolTipService.SetToolTip(delBtn, $"Delete saved tag '{tag.DisplayName}'");
            delBtn.Click += async (s, e) =>
            {
                _suppressChipClick = true;
                try
                {
                    await AppServices.CustomTags.DeleteAsync(tag.TagKey);
                    AutoMovedChipsPanel.Children.Remove(chip);
                    ToastService.Instance.ShowInfo($"Removed custom tag '{tag.DisplayName}'");
                }
                finally
                {
                    await Task.Delay(150);
                    _suppressChipClick = false;
                }
            };
            sp.Children.Add(delBtn);
        }

        chip.Content = sp;

        chip.Click += async (s, e) =>
        {
            if (_suppressChipClick) return;
            await RenameAutoMovedFileWithTagAsync(tag.TagKey, chip);
        };

        return chip;
    }

    private Button CreateAutoMovedCustomChipButton()
    {
        var customBtn = new Button
        {
            Height = 28,
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(8, 2, 8, 2)
        };

        var sp = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
        sp.Children.Add(new FontIcon { Glyph = "\uE710", FontSize = 10 });
        sp.Children.Add(new TextBlock { Text = "Custom...", FontSize = 11 });
        customBtn.Content = sp;
        ToolTipService.SetToolTip(customBtn, "Enter custom document name (saved automatically)");

        var flyout = new Flyout();
        var flyoutStack = new StackPanel { Width = 220, Spacing = 8 };
        flyoutStack.Children.Add(new TextBlock { Text = "Custom Document Name", FontWeight = FontWeights.SemiBold, FontSize = 12 });

        var tb = new TextBox { PlaceholderText = "e.g. voter_id, affidavit", FontSize = 12 };
        flyoutStack.Children.Add(tb);

        var applyBtn = new Button
        {
            Content = "Rename File",
            Style = (Style)Application.Current.Resources["AccentButtonStyle"],
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        applyBtn.Click += async (s, e) =>
        {
            var raw = tb.Text?.Trim();
            if (string.IsNullOrWhiteSpace(raw)) return;
            var saved = await SmartTagHelper.SaveCustomTagAsync(raw);
            var cleanTag = saved?.TagKey ?? SmartTagHelper.NormalizeTagKey(raw);
            flyout.Hide();
            await RenameAutoMovedFileWithTagAsync(cleanTag, customBtn);
        };

        flyoutStack.Children.Add(applyBtn);
        flyout.Content = flyoutStack;
        customBtn.Flyout = flyout;

        return customBtn;
    }

    private async Task RenameAutoMovedFileWithTagAsync(string tagKey, Button? clickedChip)
    {
        if (string.IsNullOrWhiteSpace(_currentAutoMovedDestPath)) return;

        var (success, newPath) = await AppServices.FileWatcher.RenameAutoMovedFileAsync(_currentAutoMovedDestPath, tagKey);
        if (success)
        {
            _currentAutoMovedDestPath = newPath;
            var renamedName = Path.GetFileName(newPath);

            if (SmartTagHelper.IsPhotoTag(tagKey) && SmartTagHelper.IsImageFile(newPath))
            {
                var targetSession = _activeSessions.FirstOrDefault(s => s.Customer.Name.Equals(_currentAutoMovedCustomerName, StringComparison.OrdinalIgnoreCase));
                if (targetSession != null)
                {
                    await SmartTagHelper.TryAssignCustomerPhotoAsync(targetSession.Customer, newPath);
                }
            }

            TxtAutoMovedFileName.Text = renamedName;
            TxtAutoMovedTarget.Text = $"Renamed to '{renamedName}' in {_currentAutoMovedCustomerName}'s folder ✓";

            TxtAutoMovedTarget.Foreground = (Brush)Application.Current.Resources["SystemFillColorSuccessBrush"];

            if (clickedChip != null)
            {
                clickedChip.Background = (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"];
                clickedChip.Foreground = new SolidColorBrush(Colors.White);
                if (clickedChip.Content is StackPanel sp && sp.Children.Count > 0 && sp.Children[0] is FontIcon icon)
                {
                    icon.Foreground = new SolidColorBrush(Colors.White);
                    icon.Glyph = "\uE73E";
                }
            }

            MainWindow.Instance?.NotifyIncomingFileRouted();
            ToastService.Instance.ShowSuccess($"Renamed to '{renamedName}'");

            // Give user 2.5 seconds to see confirmation before dismiss
            _undoRemainingSeconds = Math.Min(_undoRemainingSeconds, 2.5);
        }
        else
        {
            ToastService.Instance.ShowError("Could not rename auto-moved file.");
        }
    }

    public void HideWidget()
    {
        _countdownTimer.Stop();
        _undoTimer.Stop();
        IsWidgetVisible = false;
        _currentFile = null;
        _isPointerOver = false;
        _selectedTag = null;
        _selectedChipBtn = null;
        _currentAutoMovedDestPath = null;
        _currentAutoMovedCustomerName = null;
        _currentAutoMovedSession = null;
        AppWindow.Hide();
    }

    private void PositionWidget(int heightDip = 265)
    {
        var hwnd = Win32Interop.GetWindowFromWindowId(AppWindow.Id);
        var dpi = GetDpiForWindow(hwnd);
        var scale = (dpi == 0 ? 96.0 : dpi) / 96.0;

        int widthDip = 420;
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
                if (tbResult != IntPtr.Zero && abd.rc.Top > 0 && abd.rc.Top < screenH)
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

        // Clamp positions so the widget is always 100% within the visible screen area
        posX = Math.Max(0, Math.Min(screenW - widthPx, posX));
        posY = Math.Max(0, Math.Min(screenH - heightPx, posY));

        AppWindow.MoveAndResize(new RectInt32(posX, posY, widthPx, heightPx));
        RemoveWindowFrameStyles(hwnd);
        ApplyDwmWindowAttributes(hwnd);
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
