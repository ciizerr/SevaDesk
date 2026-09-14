using System.Runtime.InteropServices;
using Windows.Graphics;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SevaDesk_App.Pages;
using SevaDesk_App.Services;
using SevaDesk_App.Views;

namespace SevaDesk_App;

public sealed partial class MainWindow : Window
{
    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hWnd);

    public MainWindow()
    {
        InitializeComponent();

        AppServices.Initialize();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
        AppWindow.SetIcon("Assets/AppIcon.ico");

        // Window Sizing according to WinUI 3 rubric
        var hwnd = Win32Interop.GetWindowFromWindowId(AppWindow.Id);
        var dpi = GetDpiForWindow(hwnd);
        var scale = (dpi == 0 ? 96.0 : dpi) / 96.0;
        AppWindow.Resize(new SizeInt32((int)(1260 * scale), (int)(840 * scale)));

        NavFrame.Navigate(typeof(DashboardPage));
    }

    private void TitleBar_PaneToggleRequested(TitleBar sender, object args)
    {
        NavView.IsPaneOpen = !NavView.IsPaneOpen;
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
        if (args.IsSettingsSelected)
        {
            NavFrame.Navigate(typeof(SettingsPage));
        }
        else if (args.SelectedItem is NavigationViewItem item)
        {
            switch (item.Tag?.ToString())
            {
                case "dashboard":
                    NavFrame.Navigate(typeof(DashboardPage));
                    break;
                case "sessions":
                    NavFrame.Navigate(typeof(SessionsPage));
                    break;
                case "documents":
                    NavFrame.Navigate(typeof(DocumentsPage));
                    break;
                case "payments":
                    NavFrame.Navigate(typeof(PaymentsPage));
                    break;
                case "applications":
                    NavFrame.Navigate(typeof(ApplicationsPage));
                    break;
                case "resources":
                    NavFrame.Navigate(typeof(ResourcesPage));
                    break;
                case "customers":
                    NavFrame.Navigate(typeof(CustomersPage));
                    break;
                default:
                    NavFrame.Navigate(typeof(DashboardPage));
                    break;
            }
        }
    }
}
