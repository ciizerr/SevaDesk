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

    private const int DWMWA_BORDER_COLOR = 34;
    private const int DWMWA_COLOR_NONE = unchecked((int)0xFFFFFFFE);

    private CancellationTokenSource? _searchCts;
    private IncomingFileItem? _pendingFile;

    public Customer? SelectedExistingCustomer { get; private set; }

    public NewSessionWidget(IncomingFileItem? pendingFile = null)
    {
        InitializeComponent();
        _pendingFile = pendingFile;

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
