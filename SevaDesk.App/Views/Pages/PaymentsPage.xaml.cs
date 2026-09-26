using Microsoft.Graphics.Canvas.Text;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using SevaDesk.Core.Models;
using SevaDesk_App.Services;
using SevaDesk_App.ViewModels.Pages;
using Windows.Foundation;
using Windows.UI;
using SevaDesk_App.Views.Dialogs;

namespace SevaDesk_App.Views.Pages;

public sealed partial class PaymentsPage : Page
{
    public PaymentsViewModel ViewModel { get; } = PaymentsViewModel.SharedInstance;

    public PaymentsPage()
    {
        NavigationCacheMode = Microsoft.UI.Xaml.Navigation.NavigationCacheMode.Required;
        InitializeComponent();

        ViewModel.RequestChartRedraw += () =>
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                EarningsCanvas?.Invalidate();
            });
        };

        ViewModel.ServiceCategories.CollectionChanged += (s, e) =>
        {
            DispatcherQueue.TryEnqueue(RenderCategoryChips);
        };

        ViewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(PaymentsViewModel.QrCodeBitmap))
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    if (InlineQrImage != null)
                    {
                        InlineQrImage.Source = ViewModel.QrCodeBitmap;
                    }
                });
            }
            else if (e.PropertyName == nameof(PaymentsViewModel.SelectedCategory))
            {
                DispatcherQueue.TryEnqueue(UpdateCategoryChipStyles);
            }
            else if (e.PropertyName == nameof(PaymentsViewModel.CatalogSelectedCategory))
            {
                DispatcherQueue.TryEnqueue(UpdateCatalogCategoryChipStyles);
            }
            else if (e.PropertyName == nameof(PaymentsViewModel.FormGlyph))
            {
                DispatcherQueue.TryEnqueue(UpdateCatalogIconPicker);
            }
            else if (e.PropertyName == nameof(PaymentsViewModel.SelectedViewIndex))
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    if (ViewSelector != null)
                    {
                        ViewSelector.SelectedItem = ViewModel.SelectedViewIndex switch
                        {
                            0 => PosTab,
                            1 => RatesTab,
                            2 => EarningsTab,
                            _ => PosTab
                        };
                    }

                    if (ViewModel.SelectedViewIndex == 1)
                    {
                        RenderCatalogCategoryChips();
                        UpdateCatalogIconPicker();
                    }
                });
            }
        };
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        _ = InitializePageAsync();

        if (InlineQrImage != null)
        {
            InlineQrImage.Source = ViewModel.QrCodeBitmap;
        }

        if (ViewSelector != null && PosTab != null)
        {
            ViewSelector.SelectedItem = PosTab;
        }

        if (e.Parameter is BillingHandoverRequest handover)
        {
            ViewModel.ApplyBillingHandover(handover);
        }
        else
        {
            _ = ViewModel.CheckAutoSelectActiveSessionAsync();
        }
    }

    private async Task InitializePageAsync()
    {
        await ViewModel.InitializeAsync();
        RenderCategoryChips();
        RenderCatalogCategoryChips();
        UpdateCatalogIconPicker();
    }

    // --- UI Helper Converters ---
    public static string FormatRupee(decimal amount) => $"₹{amount:N0}";
    public static string FormatAmount(decimal amount) => $"₹{amount:N0}";
    public static string FormatUnitSuffix(string? unit) => string.IsNullOrWhiteSpace(unit) ? string.Empty : $"/ {unit}";
    public static bool HasStatus(string status) => !string.IsNullOrWhiteSpace(status);
    public static Visibility VisibleIf(bool condition) => condition ? Visibility.Visible : Visibility.Collapsed;
    public static Visibility CollapsedIf(bool condition) => condition ? Visibility.Collapsed : Visibility.Visible;
    public static Visibility CollapsedIf(int count) => count > 0 ? Visibility.Collapsed : Visibility.Visible;
    public static bool CollapsedIfBool(bool condition) => !condition;

    public Style? FilterButtonStyle(string activeFilter, string currentFilter)
    {
        if (string.Equals(activeFilter, currentFilter, StringComparison.OrdinalIgnoreCase))
        {
            return (Style)Application.Current.Resources["AccentButtonStyle"];
        }
        return null;
    }

    public Style? FlatDiscountToggleStyle(bool isPercentage)
    {
        if (!isPercentage)
        {
            if (Application.Current.Resources.TryGetValue("AccentButtonStyle", out var styleObj) && styleObj is Style style)
            {
                return style;
            }
        }
        return null;
    }

    public Style? PercentDiscountToggleStyle(bool isPercentage)
    {
        if (isPercentage)
        {
            if (Application.Current.Resources.TryGetValue("AccentButtonStyle", out var styleObj) && styleObj is Style style)
            {
                return style;
            }
        }
        return null;
    }

    // --- Navigation & Filter Handlers ---
    private void ViewSelector_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
    {
        if (sender.SelectedItem == PosTab)
        {
            if (ViewModel.SelectedViewIndex != 0)
            {
                ViewModel.SelectedViewIndex = 0;
            }
        }
        else if (sender.SelectedItem == RatesTab)
        {
            if (ViewModel.SelectedViewIndex != 1)
            {
                ViewModel.SelectedViewIndex = 1;
            }
            RenderCatalogCategoryChips();
            UpdateCatalogIconPicker();
        }
        else if (sender.SelectedItem == EarningsTab)
        {
            if (ViewModel.SelectedViewIndex != 2)
            {
                ViewModel.SelectedViewIndex = 2;
            }
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

    // --- Category Filter Chips Handlers ---
    private void CategoryFilter_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string category)
        {
            ViewModel.SelectCategory(category);
            UpdateCategoryChipStyles();
        }
    }

    private void UpdateCategoryChipStyles()
    {
        if (CategoryChipsPanel == null) return;
        foreach (var child in CategoryChipsPanel.Children)
        {
            if (child is Button b && b.Tag is string cat)
            {
                bool isSelected = string.Equals(ViewModel.SelectedCategory, cat, StringComparison.OrdinalIgnoreCase);
                b.Style = isSelected ? (Style)Application.Current.Resources["AccentButtonStyle"] : null;
            }
        }
    }

    public void RenderCategoryChips()
    {
        if (CategoryChipsPanel == null) return;
        CategoryChipsPanel.Children.Clear();

        foreach (var category in ViewModel.ServiceCategories)
        {
            var isSelected = string.Equals(ViewModel.SelectedCategory, category, StringComparison.OrdinalIgnoreCase);
            var btn = new Button
            {
                Content = category,
                Tag = category,
                Style = isSelected ? (Style)Application.Current.Resources["AccentButtonStyle"] : null,
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(14, 5, 14, 5),
                FontSize = 12
            };
            btn.Click += CategoryFilter_Click;
            CategoryChipsPanel.Children.Add(btn);
        }
    }

    private void ClearCategoryFilter_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.SelectedCategory = "All";
        ViewModel.RateCardSearchQuery = string.Empty;
        UpdateCategoryChipStyles();
    }

    // --- Catalog Category Filter Chips Handlers ---
    private void CatalogCategoryFilter_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string category)
        {
            ViewModel.SelectCatalogCategory(category);
            UpdateCatalogCategoryChipStyles();
        }
    }

    private void UpdateCatalogCategoryChipStyles()
    {
        if (CatalogCategoryChipsPanel == null) return;
        foreach (var child in CatalogCategoryChipsPanel.Children)
        {
            if (child is Button b && b.Tag is string cat)
            {
                bool isSelected = string.Equals(ViewModel.CatalogSelectedCategory, cat, StringComparison.OrdinalIgnoreCase);
                b.Style = isSelected ? (Style)Application.Current.Resources["AccentButtonStyle"] : null;
            }
        }
    }

    public void RenderCatalogCategoryChips()
    {
        if (CatalogCategoryChipsPanel == null) return;
        CatalogCategoryChipsPanel.Children.Clear();

        foreach (var category in ViewModel.ServiceCategories)
        {
            var isSelected = string.Equals(ViewModel.CatalogSelectedCategory, category, StringComparison.OrdinalIgnoreCase);
            var btn = new Button
            {
                Content = category,
                Tag = category,
                Style = isSelected ? (Style)Application.Current.Resources["AccentButtonStyle"] : null,
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(14, 5, 14, 5),
                FontSize = 12
            };
            btn.Click += CatalogCategoryFilter_Click;
            CatalogCategoryChipsPanel.Children.Add(btn);
        }
    }

    // --- Catalog Icon Picker Handlers ---
    private void CatalogGlyph_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string glyph)
        {
            ViewModel.FormGlyph = glyph;
            UpdateCatalogIconPicker();
        }
    }

    private void UpdateCatalogIconPicker()
    {
        if (CatalogIconPickerGrid == null) return;
        foreach (var child in CatalogIconPickerGrid.Children)
        {
            if (child is Button b && b.Tag is string glyph)
            {
                bool isSelected = string.Equals(ViewModel.FormGlyph, glyph, StringComparison.OrdinalIgnoreCase);
                b.Style = isSelected ? (Style)Application.Current.Resources["AccentButtonStyle"] : null;
            }
        }
    }

    // --- Catalog List Item Actions ---
    private void CatalogEdit_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is ServiceRateItem item)
        {
            ViewModel.EditRateItem(item);
            UpdateCatalogIconPicker();
        }
    }

    private async void ConfirmCatalogDelete_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is ServiceRateItem item)
        {
            await ViewModel.DeleteRateItemAsync(item);
            RenderCategoryChips();
            RenderCatalogCategoryChips();
        }
    }

    private void ManageRates_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.SwitchToRatesView();
    }

    // --- POS Cart & Payment Handlers ---
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

    private void QtyBox_GotFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox tb)
        {
            tb.SelectAll();
        }
    }

    private void QtyBox_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if (sender is TextBox tb && tb.Tag is CartItem item)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                e.Handled = true;
                CommitQuantity(tb, item);
                this.Focus(FocusState.Programmatic);
            }
            else if (e.Key == Windows.System.VirtualKey.Escape)
            {
                e.Handled = true;
                tb.Text = item.Quantity.ToString();
                this.Focus(FocusState.Programmatic);
            }
        }
    }

    private void QtyBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox tb && tb.Tag is CartItem item)
        {
            CommitQuantity(tb, item);
        }
    }

    private void CommitQuantity(TextBox tb, CartItem item)
    {
        if (int.TryParse(tb.Text, out var qty) && qty >= 1)
        {
            if (item.Quantity != qty)
            {
                ViewModel.UpdateItemQuantity(item, qty);
            }
        }
        else
        {
            ViewModel.UpdateItemQuantity(item, 1);
            tb.Text = "1";
        }
    }

    private void RemoveItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is CartItem item)
        {
            ViewModel.RemoveCartItemCommand.Execute(item);
        }
    }

    private void DiscountTypeFlat_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.SetDiscountTypeCommand.Execute(false);
    }

    private void DiscountTypePercent_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.SetDiscountTypeCommand.Execute(true);
    }

    private void ClearDiscount_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.ClearDiscountCommand.Execute(null);
    }

    private void QuickDiscount_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string tag)
        {
            ViewModel.ApplyQuickDiscountCommand.Execute(tag);
        }
    }

    private void EditRateButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement target && target.Tag is CartItem item)
        {
            FlyoutRateInput.Tag = item;
            FlyoutServiceName.Text = item.ServiceName;
            FlyoutRateInput.Text = item.Rate.ToString("N0");
            FlyoutResetBtn.Visibility = item.IsRateModified ? Visibility.Visible : Visibility.Collapsed;
            RateEditFlyout.ShowAt(target);
            FlyoutRateInput.SelectAll();
        }
    }

    private void FlyoutRateInput_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            e.Handled = true;
            CommitFlyoutRate();
        }
    }

    private void FlyoutApply_Click(object sender, RoutedEventArgs e)
    {
        CommitFlyoutRate();
    }

    private void CommitFlyoutRate()
    {
        if (FlyoutRateInput.Tag is CartItem item &&
            decimal.TryParse(FlyoutRateInput.Text, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var newRate) && newRate >= 0)
        {
            ViewModel.UpdateItemRate(item, newRate);
            RateEditFlyout.Hide();
        }
    }

    private void FlyoutReset_Click(object sender, RoutedEventArgs e)
    {
        if (FlyoutRateInput.Tag is CartItem item)
        {
            ViewModel.ResetItemRate(item);
            RateEditFlyout.Hide();
        }
    }

    private async void PayCash_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.CompletePaymentAsync("Cash");
    }

    private async void PayUpi_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.GrandTotal <= 0)
        {
            ViewModel.ShowWarning("Please add at least one service item to the bill first.");
            return;
        }

        var dialog = new UpiQrDialog(ViewModel.ShopUpiId, ViewModel.PayeeName, ViewModel.GrandTotal, ViewModel.CustomerName);
        dialog.XamlRoot = this.XamlRoot;
        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            await ViewModel.CompletePaymentAsync("UPI");
        }
    }

    private async void ShowQrDialog_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new UpiQrDialog(ViewModel.ShopUpiId, ViewModel.PayeeName, ViewModel.GrandTotal, ViewModel.CustomerName);
        dialog.XamlRoot = this.XamlRoot;
        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary && ViewModel.GrandTotal > 0)
        {
            await ViewModel.CompletePaymentAsync("UPI");
        }
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

    private async void CustomerAutoSuggest_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
        {
            var query = sender.Text.Trim();
            if (query.Length >= 1)
            {
                var customers = await AppServices.Customers.SearchAsync(query);
                sender.ItemsSource = customers.Take(8).ToList();
            }
            else
            {
                sender.ItemsSource = null;
                ViewModel.CurrentCustomerId = null;
            }
        }
    }

    private void CustomerAutoSuggest_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
    {
        if (args.SelectedItem is Customer customer)
        {
            sender.Text = customer.Name;
            ViewModel.CustomerName = customer.Name;
            ViewModel.CurrentCustomerId = customer.Id;

            var matchingActive = ViewModel.ActiveSessions.FirstOrDefault(s => s.Customer.Id == customer.Id);
            ViewModel.CurrentSessionId = matchingActive?.Session.Id;
        }
    }

    private void SelectActiveSessionChip_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is ActiveSessionItem session)
        {
            ViewModel.SelectActiveSession(session, autoSelected: false);
        }
    }

    private void UnlinkSession_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.UnlinkSession();
    }
}
