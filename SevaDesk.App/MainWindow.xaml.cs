using System.Runtime.InteropServices;
using Windows.Graphics;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using SevaDesk_App.Services;
using SevaDesk_App.ViewModels.Pages;
using SevaDesk_App.Views.Pages;
using SevaDesk.Core.Models;
using System.IO;

using SevaDesk_App.Views.Flyouts;

namespace SevaDesk_App;

public sealed partial class MainWindow : Window
{
    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

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

            // Initialize Desktop Sidebar Widget
            DesktopSidebarWidget.Instance.ShowSidebar();
            AppWindow.Closing += AppWindow_Closing;

            RootGrid.Loaded += (s, e) => 
            {
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                PickerService.Initialize(hwnd);
                DialogService.Initialize(RootGrid.XamlRoot);
            };

            // Initialize Global File Watcher Listener
            InitializeFileWatcherListener();

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

        foreach (var item in NavView.MenuItems.Concat(NavView.FooterMenuItems))
        {
            if (item is NavigationViewItem navItem)
            {
                var matches = navItem.Tag?.ToString() switch
                {
                    "dashboard"    => currentPageType == typeof(DashboardPage),
                    "sessions"     => currentPageType == typeof(SessionsPage),
                    "documents"    => currentPageType == typeof(DocumentsPage),
                    "payments"     => currentPageType == typeof(PaymentsPage),
                    "applications" => currentPageType == typeof(ApplicationsPage),
                    "resources"    => currentPageType == typeof(ResourcesPage),
                    "customers"    => currentPageType == typeof(CustomersPage),
                    "about"        => currentPageType == typeof(AboutPage),
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
                "dashboard"    => typeof(DashboardPage),
                "sessions"     => typeof(SessionsPage),
                "documents"    => typeof(DocumentsPage),
                "payments"     => typeof(PaymentsPage),
                "applications" => typeof(ApplicationsPage),
                "resources"    => typeof(ResourcesPage),
                "customers"    => typeof(CustomersPage),
                "about"        => typeof(AboutPage),
                _              => typeof(DashboardPage)
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

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    public void RestoreWindow()
    {
        AppWindow.Show();
        var hwnd = Win32Interop.GetWindowFromWindowId(AppWindow.Id);
        SetForegroundWindow(hwnd);
    }

    public void NavigateTo(Type pageType, object? parameter = null)
    {
        NavFrame.Navigate(pageType, parameter);
    }

    public void GoBack()
    {
        if (NavFrame.CanGoBack)
        {
            NavFrame.GoBack();
        }
    }

    private async void AppWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        var behavior = SettingsViewModel.CloseActionBehavior;
        if (behavior == 1) // Hide window, keep sidebar
        {
            args.Cancel = true;
            AppWindow.Hide();
            return;
        }

        if (behavior == 2) // Exit completely
        {
            DesktopSidebarWidget.Instance.Close();
            return;
        }

        // Behavior == 0: Always Prompt
        args.Cancel = true;
        await ShowCloseConfirmationDialogAsync();
    }

    private async Task ShowCloseConfirmationDialogAsync()
    {
        var chkRemember = new CheckBox
        {
            Content = AppServices.Localization.GetString("Dialog.RememberChoice"),
            Margin = new Thickness(0, 12, 0, 0)
        };

        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(new TextBlock
        {
            Text = AppServices.Localization.GetString("Dialog.ClosePrompt"),
            TextWrapping = TextWrapping.Wrap,
            FontSize = 13
        });
        panel.Children.Add(chkRemember);

        var dialog = new ContentDialog
        {
            XamlRoot = Content.XamlRoot,
            Title = AppServices.Localization.GetString("Dialog.CloseTitle"),
            Content = panel,
            PrimaryButtonText = AppServices.Localization.GetString("Dialog.MinimizeToTray"),
            SecondaryButtonText = AppServices.Localization.GetString("Dialog.ExitApp"),
            CloseButtonText = AppServices.Localization.GetString("Common.Cancel"),
            DefaultButton = ContentDialogButton.Primary
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            if (chkRemember.IsChecked == true)
            {
                SettingsViewModel.CloseActionBehavior = 1;
            }
            AppWindow.Hide();
        }
        else if (result == ContentDialogResult.Secondary)
        {
            if (chkRemember.IsChecked == true)
            {
                SettingsViewModel.CloseActionBehavior = 2;
            }
            DesktopSidebarWidget.Instance.Close();
            Application.Current.Exit();
        }
    }

    #region Global Incoming File Triage

    private IncomingFileItem? _currentIncomingFile;

    private void InitializeFileWatcherListener()
    {
        AppServices.FileWatcher.FileDetected += FileWatcher_FileDetected;
    }

    private void FileWatcher_FileDetected(IncomingFileItem item)
    {
        DispatcherQueue.TryEnqueue(async () =>
        {
            var hwnd = Win32Interop.GetWindowFromWindowId(AppWindow.Id);
            bool isMinimized = IsIconic(hwnd);
            bool isBackground = GetForegroundWindow() != hwnd;

            if ((isMinimized || isBackground) && SettingsViewModel.IsOverlayWidgetEnabledSetting)
            {
                IncomingFileOverlayWidget.Instance.ShowForFile(item);
            }
            else
            {
                _currentIncomingFile = item;
                await UpdateTriageBarAsync();
            }
        });
    }

    public void NavigateTo(Type pageType)
    {
        NavFrame.Navigate(pageType);
    }

    public void NotifyIncomingFileRouted()
    {
        GlobalTriageBar.IsOpen = false;
        if (NavFrame.Content is SessionsPage sessionsPage)
        {
            sessionsPage.ViewModel.RefreshApplicationFolders();
        }
        else if (NavFrame.Content is DocumentsPage docsPage)
        {
            _ = docsPage.ViewModel.LoadDocumentsAsync();
        }
    }

    private async Task UpdateTriageBarAsync()
    {
        if (_currentIncomingFile == null)
        {
            GlobalTriageBar.IsOpen = false;
            return;
        }

        GlobalTriageBar.Severity = InfoBarSeverity.Informational;
        GlobalTriageBar.Title = AppServices.Localization.GetString("Triage.DetectedTitle") ?? "Incoming File Detected";

        string sizeStr = _currentIncomingFile.FileSize > 1024 * 1024
            ? $"{_currentIncomingFile.FileSize / (1024.0 * 1024.0):F1} MB"
            : $"{Math.Max(1, _currentIncomingFile.FileSize / 1024)} KB";

        var sourceName = Path.GetFileName(_currentIncomingFile.SourceFolder);
        GlobalTriageBar.Message = $"{_currentIncomingFile.FileName} ({sourceName} • {sizeStr})";

        var activeSessions = (await AppServices.Sessions.GetActiveSessionsAsync()).ToList();

        if (activeSessions.Count == 1)
        {
            var singleSession = activeSessions[0];
            BtnTriageAction.Flyout = null;
            TxtTriageAction.Text = string.Format(AppServices.Localization.GetString("Triage.MoveToSingle") ?? "Move to {0}", singleSession.Customer.Name);
            BtnTriageAction.Tag = singleSession;
            BtnTriageAction.Visibility = Visibility.Visible;
        }
        else if (activeSessions.Count > 1)
        {
            TxtTriageAction.Text = AppServices.Localization.GetString("Triage.SelectCustomer") ?? "Move to Customer...";
            BtnTriageAction.Tag = null;
            BtnTriageAction.Visibility = Visibility.Visible;

            var flyout = new MenuFlyout();
            foreach (var session in activeSessions)
            {
                var s = session;
                var menuItem = new MenuFlyoutItem
                {
                    Text = $"{s.Customer.Name} ({s.Customer.Code})",
                    Icon = new FontIcon { Glyph = "\uE77B" }
                };
                menuItem.Click += async (sender, args) =>
                {
                    await RouteIncomingFileToSessionAsync(s);
                };
                flyout.Items.Add(menuItem);
            }
            BtnTriageAction.Flyout = flyout;
        }
        else
        {
            // No active customer sessions
            BtnTriageAction.Flyout = null;
            TxtTriageAction.Text = AppServices.Localization.GetString("Triage.CreateSession", "Create a new session?");
            BtnTriageAction.Tag = "new_session";
            BtnTriageAction.Visibility = Visibility.Visible;
        }

        GlobalTriageBar.IsOpen = true;
    }

    private async void TriageAction_Click(object sender, RoutedEventArgs e)
    {
        if (BtnTriageAction.Tag is ActiveSessionItem session)
        {
            await RouteIncomingFileToSessionAsync(session);
        }
        else if (BtnTriageAction.Tag is string tag && tag == "documents")
        {
            GlobalTriageBar.IsOpen = false;
            NavFrame.Navigate(typeof(DocumentsPage));
        }
        else if (BtnTriageAction.Tag is string tag2 && tag2 == "new_session")
        {
            GlobalTriageBar.IsOpen = false;
            var dialog = new SevaDesk_App.Views.Dialogs.NewCustomerDialog
            {
                XamlRoot = this.Content.XamlRoot
            };
            
            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(dialog.CustomerName))
            {
                var customer = dialog.SelectedExistingCustomer;
                if (customer == null)
                {
                    var matches = (await AppServices.Customers.SearchAsync(dialog.CustomerName)).ToList();
                    customer = matches.FirstOrDefault(c => string.Equals(c.Name, dialog.CustomerName, StringComparison.OrdinalIgnoreCase));
                    if (customer == null)
                    {
                        customer = await AppServices.Customers.CreateAsync(new Customer
                        {
                            Name = dialog.CustomerName,
                            Mobile = string.IsNullOrWhiteSpace(dialog.Mobile) ? null : dialog.Mobile,
                            Village = string.IsNullOrWhiteSpace(dialog.Village) ? null : dialog.Village,
                            IdReference = string.IsNullOrWhiteSpace(dialog.IdRef) ? null : dialog.IdRef,
                            Notes = string.IsNullOrWhiteSpace(dialog.Notes) ? null : dialog.Notes
                        });
                    }
                }
                var newSession = await AppServices.Sessions.StartSessionAsync(customer.Id, dialog.Notes);
                var folder = AppServices.FolderManager.EnsureCustomerWorkingFolder(customer.Name, customer.Code);
                newSession.FolderPath = folder;
                newSession.Customer = customer;

                if (_currentIncomingFile != null)
                {
                    await AppServices.FileWatcher.RouteFileToCustomerAsync(_currentIncomingFile.FilePath, folder, deleteSource: true);
                }
                
                _currentIncomingFile = null;
                NotifyIncomingFileRouted();
                NavFrame.Navigate(typeof(SessionsPage));
            }
        }
    }

    private async Task RouteIncomingFileToSessionAsync(ActiveSessionItem session)
    {
        if (_currentIncomingFile == null) return;
        var file = _currentIncomingFile;

        var success = await AppServices.FileWatcher.RouteFileToCustomerAsync(file.FilePath, session.FolderPath, deleteSource: true);
        if (success)
        {
            GlobalTriageBar.Severity = InfoBarSeverity.Success;
            GlobalTriageBar.Title = AppServices.Localization.GetString("Common.Success") ?? "Success";
            GlobalTriageBar.Message = string.Format(AppServices.Localization.GetString("Triage.SuccessMoved") ?? "Moved '{0}' to {1}'s folder.", file.FileName, session.Customer.Name);
            BtnTriageAction.Visibility = Visibility.Collapsed;

            // Notify active page if it is SessionsPage or DocumentsPage
            if (NavFrame.Content is SessionsPage sessionsPage)
            {
                sessionsPage.ViewModel.RefreshApplicationFolders();
            }
            else if (NavFrame.Content is DocumentsPage docsPage)
            {
                _ = docsPage.ViewModel.LoadDocumentsAsync();
            }

            _currentIncomingFile = null;

            // Auto-hide after 3.5 seconds
            await Task.Delay(3500);
            if (_currentIncomingFile == null)
            {
                GlobalTriageBar.IsOpen = false;
            }
        }
    }

    private void GlobalTriageBar_CloseButtonClick(InfoBar sender, object args)
    {
        _currentIncomingFile = null;
    }

    #endregion
}
