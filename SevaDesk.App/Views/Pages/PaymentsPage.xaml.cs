using Microsoft.Graphics.Canvas.Text;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using SevaDesk.Core.Models;
using SevaDesk_App.ViewModels.Pages;
using Windows.Foundation;
using Windows.UI;

namespace SevaDesk_App.Views.Pages;

public sealed partial class PaymentsPage : Page
{
    public PaymentsViewModel ViewModel { get; } = new();

    public PaymentsPage()
    {
        InitializeComponent();

        ViewModel.RequestChartRedraw += () =>
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                EarningsCanvas?.Invalidate();
            });
        };
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        _ = ViewModel.InitializeAsync();

        if (ViewSelector != null && PosTab != null)
        {
            ViewSelector.SelectedItem = PosTab;
        }

        if (e.Parameter is BillingHandoverRequest handover)
        {
            ViewModel.ApplyBillingHandover(handover);
        }
    }

    // --- UI Helper Converters ---
    public static string FormatRupee(decimal amount) => $"₹{amount:N0}";
    public static string FormatAmount(decimal amount) => $"₹{amount:N0}";
    public static bool HasStatus(string status) => !string.IsNullOrWhiteSpace(status);
    public static Visibility VisibleIf(bool condition) => condition ? Visibility.Visible : Visibility.Collapsed;
    public static Visibility CollapsedIf(bool condition) => condition ? Visibility.Collapsed : Visibility.Visible;
    public static Visibility CollapsedIf(int count) => count > 0 ? Visibility.Collapsed : Visibility.Visible;
    public static bool CollapsedIfBool(bool condition) => !condition;

    public Style FilterButtonStyle(string activeFilter, string currentFilter)
    {
        if (string.Equals(activeFilter, currentFilter, StringComparison.OrdinalIgnoreCase))
        {
            return (Style)Application.Current.Resources["AccentButtonStyle"];
        }
        return (Style)Application.Current.Resources["DefaultButtonStyle"];
    }

    // --- Navigation & Filter Handlers ---
    private void ViewSelector_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
    {
        if (sender.SelectedItem == PosTab)
        {
            ViewModel.SelectedViewIndex = 0;
        }
        else if (sender.SelectedItem == EarningsTab)
        {
            ViewModel.SelectedViewIndex = 1;
            EarningsCanvas?.Invalidate();
        }
    }

    private void QuickFilter_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string filter)
        {
            ViewModel.SelectQuickFilter(filter);
        }
    }

    // --- Win2D Interactive Stacked Bar Chart ---
    private void EarningsCanvas_Draw(CanvasControl sender, CanvasDrawEventArgs args)
    {
        var ds = args.DrawingSession;
        var bars = ViewModel.ChartBars;
        float width = (float)sender.ActualWidth;
        float height = (float)sender.ActualHeight;

        if (width <= 10 || height <= 10) return;

        if (bars == null || bars.Count == 0)
        {
            using var emptyFormat = new CanvasTextFormat
            {
                FontSize = 13,
                HorizontalAlignment = CanvasHorizontalAlignment.Center,
                VerticalAlignment = CanvasVerticalAlignment.Center
            };
            ds.DrawText("No payment transactions recorded for this period", new Rect(0, 0, width, height), Color.FromArgb(140, 148, 163, 184), emptyFormat);
            return;
        }

        float bottomMargin = 26f;
        float topMargin = 22f;
        float chartHeight = height - bottomMargin - topMargin;
        if (chartHeight <= 10) return;

        decimal maxVal = bars.Max(b => b.Total);
        if (maxVal <= 0) maxVal = 100;

        int count = bars.Count;
        float slotWidth = width / count;
        float barWidth = Math.Clamp(slotWidth * 0.55f, 8f, 36f);

        // Draw horizontal grid lines
        var gridLineColor = Color.FromArgb(20, 200, 200, 200);
        for (int i = 1; i <= 3; i++)
        {
            float y = topMargin + chartHeight * (1f - (float)i / 3f);
            ds.DrawLine(0, y, width, y, gridLineColor, 1f);
        }

        using var labelFormat = new CanvasTextFormat
        {
            FontSize = 10,
            HorizontalAlignment = CanvasHorizontalAlignment.Center,
            VerticalAlignment = CanvasVerticalAlignment.Top
        };

        using var valueFormat = new CanvasTextFormat
        {
            FontSize = 9,
            HorizontalAlignment = CanvasHorizontalAlignment.Center,
            VerticalAlignment = CanvasVerticalAlignment.Bottom
        };

        var upiColor = Color.FromArgb(240, 59, 130, 246);  // Vibrant Blue
        var cashColor = Color.FromArgb(240, 16, 185, 129); // Vibrant Emerald Green
        var textSecondary = Color.FromArgb(180, 148, 163, 184);

        float baseY = height - bottomMargin;

        // Baseline
        ds.DrawLine(0, baseY, width, baseY, Color.FromArgb(50, 200, 200, 200), 1f);

        for (int i = 0; i < count; i++)
        {
            var bar = bars[i];
            float centerX = slotWidth * i + slotWidth / 2f;
            float left = centerX - barWidth / 2f;

            float totalBarH = (float)(bar.Total / maxVal) * chartHeight;
            float cashBarH = (float)(bar.Cash / maxVal) * chartHeight;
            float upiBarH = (float)(bar.Upi / maxVal) * chartHeight;

            if (totalBarH > 0)
            {
                // Draw Cash segment at bottom
                if (cashBarH > 0)
                {
                    var cashRect = new Rect(left, baseY - cashBarH, barWidth, cashBarH);
                    ds.FillRoundedRectangle(cashRect, 3, 3, cashColor);
                }

                // Draw UPI segment stacked on top of Cash
                if (upiBarH > 0)
                {
                    var upiRect = new Rect(left, baseY - cashBarH - upiBarH, barWidth, upiBarH);
                    ds.FillRoundedRectangle(upiRect, 3, 3, upiColor);
                }

                // Value text above bar
                string valText = bar.Total >= 1000 ? $"₹{bar.Total / 1000m:F1}k" : $"₹{bar.Total:N0}";
                ds.DrawText(valText, new Rect(centerX - 28, baseY - totalBarH - 18, 56, 16), textSecondary, valueFormat);
            }
            else
            {
                // Subtle tick on baseline for empty slot
                ds.FillEllipse(centerX, baseY - 2, 2, 2, gridLineColor);
            }

            // Draw label below baseline
            bool showLabel = count <= 14 || (i % 2 == 0) || (i == count - 1);
            if (showLabel)
            {
                ds.DrawText(bar.Label, new Rect(centerX - 28, baseY + 6, 56, 18), textSecondary, labelFormat);
            }
        }
    }

    // --- POS Cart & Payment Handlers ---
    private void RateCard_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (ViewModel.IsEditMode) return;
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

    private void CopyUpi_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dataPackage = new Windows.ApplicationModel.DataTransfer.DataPackage();
            dataPackage.SetText(ViewModel.ShopUpiId);
            Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dataPackage);
            ViewModel.ShowSuccess($"UPI ID copied: {ViewModel.ShopUpiId}");
        }
        catch { }
    }
}
