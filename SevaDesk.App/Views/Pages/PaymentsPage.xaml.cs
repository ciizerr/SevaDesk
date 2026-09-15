using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
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

    public static string FormatRupee(decimal amount) => $"₹{amount:N0}";
    public static string FormatAmount(decimal amount) => $"₹{amount:N0}";
    public static bool HasStatus(string status) => !string.IsNullOrWhiteSpace(status);

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

    private void PayCash_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.CompletePaymentCommand.Execute("Cash");
    }

    private void PayUpi_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.CompletePaymentCommand.Execute("UPI");
    }
}
