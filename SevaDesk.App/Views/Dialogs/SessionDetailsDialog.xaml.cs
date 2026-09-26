using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using SevaDesk.Core.Models;
using SevaDesk_App.Services;
using SevaDesk_App.ViewModels.Pages;

namespace SevaDesk_App.Views.Dialogs;

public sealed partial class SessionDetailsDialog : ContentDialog
{
    private readonly CustomerSessionRowModel _row;
    private readonly Customer? _customer;

    public bool NavigateToBillingRequested { get; private set; }
    public BillingHandoverRequest? HandoverRequest { get; private set; }

    public static Visibility VisibleIf(bool condition) => condition ? Visibility.Visible : Visibility.Collapsed;

    public static Brush ItemBadgeBackground(bool isDiscount) =>
        isDiscount
            ? new SolidColorBrush(Color.FromArgb(35, 16, 185, 129))
            : (Brush)Application.Current.Resources["SubtleFillColorSecondaryBrush"];

    public static Brush ItemBadgeForeground(bool isDiscount) =>
        isDiscount
            ? new SolidColorBrush(Color.FromArgb(255, 16, 185, 129))
            : (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"];

    public static Brush ItemTotalForeground(bool isDiscount) =>
        isDiscount
            ? new SolidColorBrush(Color.FromArgb(255, 16, 185, 129))
            : (Brush)Application.Current.Resources["SystemFillColorSuccessBrush"];

    public SessionDetailsDialog(CustomerSessionRowModel row, Customer? customer)
    {
        InitializeComponent();
        this.EnableLightDismiss();
        _row = row;
        _customer = customer;

        PopulateDetails();
    }

    private void PopulateDetails()
    {
        TxtFormattedDate.Text = _row.FormattedDateShort;
        TxtStatus.Text = _row.Status;
        TxtDuration.Text = $"Duration: {_row.FormattedDuration}";

        var startTimeStr = _row.Session.StartedAt.ToLocalTime().ToString("hh:mm tt");
        var endTimeStr = _row.Session.EndedAt.HasValue
            ? _row.Session.EndedAt.Value.ToLocalTime().ToString("hh:mm tt")
            : "Ongoing";
        TxtTimeRange.Text = $"{startTimeStr} – {endTimeStr}";

        if (!string.IsNullOrWhiteSpace(_row.Notes))
        {
            PnlNotes.Visibility = Visibility.Visible;
            TxtNotes.Text = _row.Notes;
        }

        if (_row.HasPayment && _row.Payment != null)
        {
            PnlPaymentDetails.Visibility = Visibility.Visible;
            TxtInvoiceNo.Text = _row.InvoiceNo;
            TxtAmount.Text = $"₹{_row.Payment.Amount:N0}";
            TxtPaymentMethod.Text = _row.PaymentMethod;

            var items = BilledServiceItem.ParseSummary(_row.Payment.ItemsSummary);
            if (items.Count > 0)
            {
                ItemsSummaryList.ItemsSource = items;
                ItemsSummaryList.Visibility = Visibility.Visible;
                TxtItemsCount.Text = items.Count == 1 ? "1 item" : $"{items.Count} items";
                BadgeItemsCount.Visibility = Visibility.Visible;
                TxtItemsSummaryFallback.Visibility = Visibility.Collapsed;
            }
            else
            {
                ItemsSummaryList.Visibility = Visibility.Collapsed;
                BadgeItemsCount.Visibility = Visibility.Collapsed;
                TxtItemsSummaryFallback.Text = string.IsNullOrWhiteSpace(_row.Payment.ItemsSummary)
                    ? "General Session"
                    : _row.Payment.ItemsSummary;
                TxtItemsSummaryFallback.Visibility = Visibility.Visible;
            }
        }
        else
        {
            PnlUnbilled.Visibility = Visibility.Visible;
        }
    }

    public bool DeleteRequested { get; private set; }

    private void BillThisSession_Click(object sender, RoutedEventArgs e)
    {
        NavigateToBillingRequested = true;
        HandoverRequest = new BillingHandoverRequest
        {
            CustomerId = _customer?.Id ?? _row.Session.CustomerId,
            CustomerName = _customer?.Name ?? "Customer",
            SessionId = _row.Session.Id,
            Items = []
        };

        Hide();
    }

    private void DeletePrompt_Click(object sender, RoutedEventArgs e)
    {
        BtnDeletePrompt.Visibility = Visibility.Collapsed;
        PnlDeleteConfirm.Visibility = Visibility.Visible;
    }

    private void CancelDelete_Click(object sender, RoutedEventArgs e)
    {
        PnlDeleteConfirm.Visibility = Visibility.Collapsed;
        BtnDeletePrompt.Visibility = Visibility.Visible;
    }

    private void ConfirmDelete_Click(object sender, RoutedEventArgs e)
    {
        DeleteRequested = true;
        Hide();
    }
}
