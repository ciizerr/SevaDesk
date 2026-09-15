using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using SevaDesk_App.ViewModels.Pages;
using SevaDesk.Core.Models;

namespace SevaDesk_App.Views.Pages;

public sealed partial class PaymentsPage : Page
{
    public PaymentsViewModel ViewModel { get; } = new();

    public PaymentsPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is BillingHandoverRequest handover)
        {
            ViewModel.ApplyBillingHandover(handover);
        }
    }

    public static string FormatRupee(decimal amount) => $"₹{amount:N0}";
    public static string FormatAmount(decimal amount) => $"₹{amount:N0}";
    public static bool HasStatus(string status) => !string.IsNullOrWhiteSpace(status);
    public static Visibility VisibleIf(bool condition) => condition ? Visibility.Visible : Visibility.Collapsed;
    public static Visibility CollapsedIf(bool condition) => condition ? Visibility.Collapsed : Visibility.Visible;

    private void RateCard_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is ServiceRateItem service)
        {
            ViewModel.AddToCartCommand.Execute(service);
        }
    }

    private void ClearBill_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.ClearBillCommand.Execute(null);
    }

    private void IncreaseQty_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is CartItem item)
        {
            ViewModel.IncreaseQtyCommand.Execute(item);
        }
    }

    private void DecreaseQty_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is CartItem item)
        {
            ViewModel.DecreaseQtyCommand.Execute(item);
        }
    }

    private void RemoveItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is CartItem item)
        {
            ViewModel.RemoveCartItemCommand.Execute(item);
        }
    }

    private async void PayCash_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.CompletePaymentAsync("Cash");
    }

    private async void PayUpi_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.CompletePaymentAsync("UPI");
    }
}
