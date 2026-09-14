using System.Runtime.InteropServices;
using Windows.Graphics;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using SevaDesk_App.Pages;
using SevaDesk_App.Services;
using SevaDesk_App.Views;

namespace SevaDesk_App;

public sealed partial class MainWindow : Window
{
    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hWnd);

    public static MainWindow? Instance { get; private set; }

    private ElementTheme _currentTheme = ElementTheme.Default;
    private bool _isMicaEnabled = true;
    private readonly Windows.UI.ViewManagement.UISettings _uiSettings = new();

    public ElementTheme CurrentTheme => _currentTheme;
    public bool IsMicaEnabled => _isMicaEnabled;

    public MainWindow()
    {
        try
        {
            Instance = this;
            AppServices.Initialize();
            InitializeComponent();

            ExtendsContentIntoTitleBar = true;
            SetTitleBar(AppTitleBar);
            AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
            AppWindow.SetIcon("Assets/AppIcon.ico");

            // Setup Theme & System Backdrop
            UpdateTitleBarTheme(_currentTheme);
            ApplyBackdropAndBackground();

            _uiSettings.ColorValuesChanged += UiSettings_ColorValuesChanged;
            NavFrame.Navigated += NavFrame_Navigated;

            // Window Sizing according to WinUI 3 rubric
            var hwnd = Win32Interop.GetWindowFromWindowId(AppWindow.Id);
            var dpi = GetDpiForWindow(hwnd);
            var scale = (dpi == 0 ? 96.0 : dpi) / 96.0;
            AppWindow.Resize(new SizeInt32((int)(1260 * scale), (int)(840 * scale)));

            NavFrame.Navigate(typeof(DashboardPage));
            NavFrame.BackStack.Clear();
        }
        catch (Exception ex)
        {
            try
            {
                System.IO.File.WriteAllText(System.IO.Path.Combine(System.AppContext.BaseDirectory, "crash_mainwindow.txt"),
                    $"{ex.Message}\n{ex}\n{ex.StackTrace}");
            }
            catch { }
            throw;
        }
    }

    private void UiSettings_ColorValuesChanged(Windows.UI.ViewManagement.UISettings sender, object args)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            if (_currentTheme == ElementTheme.Default)
            {
                UpdateTitleBarTheme(ElementTheme.Default);
                ApplyBackdropAndBackground();
            }
        });
    }

    private void NavFrame_Navigated(object sender, NavigationEventArgs e)
    {
        AppTitleBar.IsBackButtonVisible = NavFrame.CanGoBack;
        SyncNavViewSelection(e.SourcePageType);
    }

    private void SyncNavViewSelection(Type currentPageType)
    {
        if (currentPageType == typeof(SettingsPage))
        {
            NavView.SelectedItem = NavView.SettingsItem;
            return;
        }

        foreach (var item in NavView.MenuItems)
        {
            if (item is NavigationViewItem navItem)
            {
                var matches = navItem.Tag?.ToString() switch
                {
                    "dashboard" => currentPageType == typeof(DashboardPage),
                    "sessions" => currentPageType == typeof(SessionsPage),
                    "documents" => currentPageType == typeof(DocumentsPage),
                    "payments" => currentPageType == typeof(PaymentsPage),
                    "applications" => currentPageType == typeof(ApplicationsPage),
                    "resources" => currentPageType == typeof(ResourcesPage),
                    "customers" => currentPageType == typeof(CustomersPage),
                    _ => false
                };

                if (matches)
                {
                    NavView.SelectedItem = navItem;
                    return;
                }
            }
        }
    }

    private void TitleBar_BackRequested(TitleBar sender, object args)
    {
        if (NavFrame.CanGoBack)
        {
            NavFrame.GoBack();
        }
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        Type? targetType = null;
        if (args.IsSettingsSelected)
        {
            targetType = typeof(SettingsPage);
        }
        else if (args.SelectedItem is NavigationViewItem item)
        {
            targetType = item.Tag?.ToString() switch
            {
                "dashboard" => typeof(DashboardPage),
                "sessions" => typeof(SessionsPage),
                "documents" => typeof(DocumentsPage),
                "payments" => typeof(PaymentsPage),
                "applications" => typeof(ApplicationsPage),
                "resources" => typeof(ResourcesPage),
                "customers" => typeof(CustomersPage),
                _ => typeof(DashboardPage)
            };
        }

        if (targetType != null && NavFrame.CurrentSourcePageType != targetType)
        {
            NavFrame.Navigate(targetType);
            NavFrame.BackStack.Clear();
            AppTitleBar.IsBackButtonVisible = false;
        }
    }

    public void SetTheme(ElementTheme theme)
    {
        _currentTheme = theme;
        if (Content is FrameworkElement root)
        {
            root.RequestedTheme = theme;
        }
        UpdateTitleBarTheme(theme);
        ApplyBackdropAndBackground();
    }

    public void SetMicaBackdrop(bool enabled)
    {
        _isMicaEnabled = enabled;
        ApplyBackdropAndBackground();
    }

    public void UpdateTitleBarTheme(ElementTheme theme)
    {
        if (!AppWindowTitleBar.IsCustomizationSupported()) return;

        var titleBar = AppWindow.TitleBar;

        bool isDark = theme switch
        {
            ElementTheme.Dark => true,
            ElementTheme.Light => false,
            _ => Application.Current.RequestedTheme == ApplicationTheme.Dark
        };

        titleBar.ButtonBackgroundColor = Microsoft.UI.Colors.Transparent;
        titleBar.ButtonInactiveBackgroundColor = Microsoft.UI.Colors.Transparent;

        if (isDark)
        {
            titleBar.ButtonForegroundColor = Microsoft.UI.Colors.White;
            titleBar.ButtonHoverForegroundColor = Microsoft.UI.Colors.White;
            titleBar.ButtonHoverBackgroundColor = Windows.UI.Color.FromArgb(30, 255, 255, 255);
            titleBar.ButtonPressedForegroundColor = Microsoft.UI.Colors.White;
            titleBar.ButtonPressedBackgroundColor = Windows.UI.Color.FromArgb(50, 255, 255, 255);
            titleBar.ButtonInactiveForegroundColor = Windows.UI.Color.FromArgb(120, 255, 255, 255);
        }
        else
        {
            titleBar.ButtonForegroundColor = Windows.UI.Color.FromArgb(255, 25, 25, 25);
            titleBar.ButtonHoverForegroundColor = Microsoft.UI.Colors.Black;
            titleBar.ButtonHoverBackgroundColor = Windows.UI.Color.FromArgb(20, 0, 0, 0);
            titleBar.ButtonPressedForegroundColor = Microsoft.UI.Colors.Black;
            titleBar.ButtonPressedBackgroundColor = Windows.UI.Color.FromArgb(40, 0, 0, 0);
            titleBar.ButtonInactiveForegroundColor = Windows.UI.Color.FromArgb(120, 100, 100, 100);
        }
    }

    private void ApplyBackdropAndBackground()
    {
        if (_isMicaEnabled)
        {
            SystemBackdrop = new Microsoft.UI.Xaml.Media.MicaBackdrop();
            RootGrid.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);
        }
        else
        {
            SystemBackdrop = null;

            bool isDark = _currentTheme switch
            {
                ElementTheme.Dark => true,
                ElementTheme.Light => false,
                _ => Application.Current.RequestedTheme == ApplicationTheme.Dark
            };

            // When Mica is disabled, provide a solid theme-aware surface so the DirectX swapchain black does not bleed through
            RootGrid.Background = isDark
                ? new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 32, 32, 32))
                : new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 243, 243, 243));
        }
    }
}
