using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Graphics;
using SevaDesk.Core.Models;
using SevaDesk_App.Services;

namespace SevaDesk_App.Views.Flyouts;

public sealed partial class NewSessionWidget : Window
{
    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

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

        int darkMode = 1;
        DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkMode, sizeof(int));

        int colorNone = DWMWA_COLOR_NONE;
        DwmSetWindowAttribute(hwnd, DWMWA_BORDER_COLOR, ref colorNone, sizeof(int));

        int cornerPref = DWMWCP_ROUND;
        DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref cornerPref, sizeof(int));
    }

    private const int GWL_STYLE = -16;
    private const int WS_BORDER = 0x00800000;
    private const int WS_DLGFRAME = 0x00400000;
    private const int WS_CAPTION = 0x00C00000;
    private const int WS_THICKFRAME = 0x00040000;

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

    private CancellationTokenSource? _searchCts;
    private IncomingFileItem? _pendingFile;

    public Customer? SelectedExistingCustomer { get; private set; }

    public NewSessionWidget(IncomingFileItem? pendingFile = null)
    {
        InitializeComponent();
        _pendingFile = pendingFile;

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

        AppWindow.Closing += (sender, args) =>
        {
            var pending = _pendingFile;
            _pendingFile = null;
            if (pending != null)
            {
                IncomingFileOverlayWidget.Instance.ShowForFile(pending);
            }
        };

        PositionWidget();
        MobileBox.ConfigureNumericMobileInput(_ => ValidateInputs());
    }

    private void PositionWidget()
    {
        var hwnd = Win32Interop.GetWindowFromWindowId(AppWindow.Id);
        var dpi = GetDpiForWindow(hwnd);
        var scale = (dpi == 0 ? 96.0 : dpi) / 96.0;

        int widthPx = (int)(480 * scale);
        int heightPx = (int)(520 * scale);

        int screenW = GetSystemMetrics(0); // SM_CXSCREEN
        int screenH = GetSystemMetrics(1); // SM_CYSCREEN

        int posX = (screenW - widthPx) / 2;
        int posY = (screenH - heightPx) / 2;

        AppWindow.MoveAndResize(new RectInt32(posX, posY, widthPx, heightPx));
        RemoveWindowFrameStyles(hwnd);
        ApplyDwmWindowAttributes(hwnd);
    }

    private async void NameSuggestBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
        {
            var text = sender.Text?.Trim();

            if (SelectedExistingCustomer != null && !string.Equals(SelectedExistingCustomer.Name, text, StringComparison.OrdinalIgnoreCase))
            {
                SelectedExistingCustomer = null;
                ExistingCustomerBadge.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                sender.ItemsSource = null;
                return;
            }

            _searchCts?.Cancel();
            _searchCts = new CancellationTokenSource();
            var token = _searchCts.Token;

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

    private void NameSuggestBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
    {
        if (args.SelectedItem is Customer customer)
        {
            SelectCustomer(customer);
        }
    }

    private async void NameSuggestBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        if (args.ChosenSuggestion is Customer customer)
        {
            SelectCustomer(customer);
        }
        else if (!string.IsNullOrWhiteSpace(args.QueryText))
        {
            var text = args.QueryText.Trim();
            var matches = (await AppServices.Customers.SearchAsync(text)).ToList();
            var exact = matches.FirstOrDefault(c => string.Equals(c.Name, text, StringComparison.OrdinalIgnoreCase));
            if (exact != null)
            {
                SelectCustomer(exact);
            }
        }
    }

    private void SelectCustomer(Customer customer)
    {
        SelectedExistingCustomer = customer;
        NameSuggestBox.Text = customer.Name;
        MobileBox.Text = customer.Mobile ?? string.Empty;
        VillageBox.Text = customer.Village ?? string.Empty;
        IdRefBox.Text = customer.IdReference ?? string.Empty;

        var template = AppServices.Localization.GetString("Dialog.NewCustomer.ExistingCustomerFound");
        if (string.IsNullOrWhiteSpace(template) || template == "Dialog.NewCustomer.ExistingCustomerFound")
        {
            template = "Existing customer matched ({0}) — session will link to profile.";
        }
        TxtBadgeInfo.Text = string.Format(template, customer.Code);
        ExistingCustomerBadge.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
        ValidateInputs();
    }

    private void ValidateInputs()
    {
        var mobile = MobileBox.Text?.Trim() ?? string.Empty;
        if (mobile.Length > 0 && mobile.Length < 10)
        {
            TxtMobileError.Text = $"Please enter 10 digits ({mobile.Length}/10 entered)";
            TxtMobileError.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
            BtnStartSession.IsEnabled = false;
        }
        else
        {
            TxtMobileError.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
            BtnStartSession.IsEnabled = true;
        }
    }

    private async void StartSession_Click(object sender, RoutedEventArgs e)
    {
        var name = NameSuggestBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            NameSuggestBox.Focus(FocusState.Programmatic);
            return;
        }

        var mobile = MobileBox.Text?.Trim();
        if (!string.IsNullOrWhiteSpace(mobile) && mobile.Length != 10)
        {
            ValidateInputs();
            MobileBox.Focus(FocusState.Programmatic);
            return;
        }

        var village = VillageBox.Text?.Trim();
        var idRef = IdRefBox.Text?.Trim();
        var notes = NotesBox.Text?.Trim();

        Customer customer;
        if (SelectedExistingCustomer != null)
        {
            customer = SelectedExistingCustomer;
            bool changed = false;
            if (!string.IsNullOrWhiteSpace(mobile) && mobile != customer.Mobile)
            {
                customer.Mobile = mobile;
                changed = true;
            }
            if (!string.IsNullOrWhiteSpace(village) && village != customer.Village)
            {
                customer.Village = village;
                changed = true;
            }
            if (!string.IsNullOrWhiteSpace(idRef) && idRef != customer.IdReference)
            {
                customer.IdReference = idRef;
                changed = true;
            }
            if (changed)
            {
                await AppServices.Customers.UpdateAsync(customer);
            }
        }
        else
        {
            var matches = (await AppServices.Customers.SearchAsync(name)).ToList();
            var exact = matches.FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));
            if (exact != null)
            {
                customer = exact;
            }
            else
            {
                customer = await AppServices.Customers.CreateAsync(new Customer
                {
                    Name = name,
                    Mobile = string.IsNullOrWhiteSpace(mobile) ? null : mobile,
                    Village = string.IsNullOrWhiteSpace(village) ? null : village,
                    IdReference = string.IsNullOrWhiteSpace(idRef) ? null : idRef,
                    Notes = string.IsNullOrWhiteSpace(notes) ? null : notes
                });
            }
        }

        var newSession = await AppServices.Sessions.StartSessionAsync(customer.Id, notes);
        var folder = AppServices.FolderManager.EnsureCustomerWorkingFolder(customer.Name, customer.Code);
        newSession.FolderPath = folder;
        newSession.Customer = customer;
        newSession.FolderStats = new FolderStats();

        if (_pendingFile == null && !string.IsNullOrWhiteSpace(folder))
        {
            AppServices.FolderManager.OpenFolderInExplorer(folder);
        }

        var pending = _pendingFile;
        _pendingFile = null;
        this.Close();

        if (pending != null)
        {
            // Re-open / refresh the incoming file triage widget for this newly created customer
            // so the operator can choose a quick rename tag (e.g. [Aadhaar]) or move directly!
            IncomingFileOverlayWidget.Instance.ShowForFile(pending);
        }
    }

    private async void QuickWalkIn_Click(object sender, RoutedEventArgs e)
    {
        var pending = _pendingFile;
        _pendingFile = null;
        this.Close();

        try
        {
            var walkInSession = await AppServices.Sessions.StartSessionAsync("walk-in");
            var folder = AppServices.FolderManager.EnsureWalkInTempFolderPath();

            if (pending != null)
            {
                var (success, _) = await AppServices.FileWatcher.RouteFileToSessionWithUndoTrackingAsync(
                    pending.FilePath, folder, walkInSession.Customer.Name, walkInSession.Session.Id);

                if (success)
                {
                    MainWindow.Instance?.NotifyIncomingFileRouted();
                    ToastService.Instance.ShowSuccess("File moved to today's Walk-in temp folder");
                }
            }

            AppServices.FolderManager.OpenFolderInExplorer(folder);
        }
        catch (Exception ex)
        {
            ToastService.Instance.ShowError($"Could not start Walk-in: {ex.Message}");
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        var pending = _pendingFile;
        _pendingFile = null;
        this.Close();

        if (pending != null)
        {
            IncomingFileOverlayWidget.Instance.ShowForFile(pending);
        }
    }
}
