using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using SevaDesk.Core.Models;
using SevaDesk.Infrastructure.FileManager;
using SevaDesk_App.Services;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI;

namespace SevaDesk_App.Views.Dialogs;

public sealed partial class ReceiptDialog : ContentDialog
{
    private readonly CompletedReceiptInfo _receipt;
    private readonly string _shopName;
    private readonly string _shopAddress;
    private readonly string _shopContact;
    private bool _isPdfFormat;
    private string? _cachedJpgPath;
    private string? _cachedPdfPath;

    public ReceiptDialog(CompletedReceiptInfo receipt)
    {
        InitializeComponent();
        this.EnableLightDismiss();

        _receipt = receipt ?? throw new ArgumentNullException(nameof(receipt));

        _shopName = AppServices.Database.GetSetting("shop_name", "SevaDesk Cyber Center") ?? "SevaDesk Cyber Center";
        _shopAddress = AppServices.Database.GetSetting("shop_address", "Digital Seva & CSC Center") ?? "Digital Seva & CSC Center";
        _shopContact = AppServices.Database.GetSetting("contact_number", "") ?? "";

        TxtShopName.Text = _shopName;
        TxtShopAddress.Text = string.IsNullOrWhiteSpace(_shopAddress) ? "Digital Seva & CSC Center" : _shopAddress;
        TxtShopContact.Text = string.IsNullOrWhiteSpace(_shopContact) ? "" : $"Ph: {_shopContact}";
        TxtShopContact.Visibility = string.IsNullOrWhiteSpace(_shopContact) ? Visibility.Collapsed : Visibility.Visible;

        TxtInvoiceNo.Text = _receipt.InvoiceNo;
        TxtDateTime.Text = _receipt.PaymentDate.ToString("dd-MMM-yyyy hh:mm tt");
        TxtPaidBadge.Text = $"PAID ({(_receipt.PaymentMode ?? "CASH").ToUpperInvariant()})";

        TxtCustomerName.Text = string.IsNullOrWhiteSpace(_receipt.CustomerName) ? "Walk-in Customer" : _receipt.CustomerName;
        if (!string.IsNullOrWhiteSpace(_receipt.CustomerMobile))
        {
            TxtCustomerPhone.Text = _receipt.CustomerMobile;
            TxtCustomerPhone.Visibility = Visibility.Visible;
        }
        else
        {
            TxtCustomerPhone.Visibility = Visibility.Collapsed;
        }

        if (!string.IsNullOrWhiteSpace(_receipt.CustomerAddress))
        {
            TxtCustomerAddress.Text = $"Address: {_receipt.CustomerAddress.Trim()}";
            TxtCustomerAddress.Visibility = Visibility.Visible;
        }
        else
        {
            TxtCustomerAddress.Visibility = Visibility.Collapsed;
        }

        TxtMobileInput.Text = _receipt.CustomerMobile ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(_receipt.CustomerMobile) && _receipt.CustomerMobile.Trim().Length == 10)
        {
            MobileVerifiedBadge.Visibility = Visibility.Visible;
        }

        TxtMobileInput.TextChanged += (s, e) =>
        {
            var raw = TxtMobileInput.Text?.Trim() ?? string.Empty;
            var digits = new string(raw.Where(char.IsDigit).ToArray());
            MobileVerifiedBadge.Visibility = digits.Length == 10 ? Visibility.Visible : Visibility.Collapsed;
            if (digits.Length > 0)
            {
                TxtCustomerPhone.Text = digits;
                TxtCustomerPhone.Visibility = Visibility.Visible;
            }
            else if (!string.IsNullOrWhiteSpace(_receipt.CustomerMobile))
            {
                TxtCustomerPhone.Text = _receipt.CustomerMobile;
                TxtCustomerPhone.Visibility = Visibility.Visible;
            }
            else
            {
                TxtCustomerPhone.Visibility = Visibility.Collapsed;
            }
        };

        try
        {
            var legacyDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SevaDesk", "Receipts");
            if (Directory.Exists(legacyDir))
            {
                Directory.Delete(legacyDir, recursive: true);
            }
        }
        catch { }
        PurgeOldTempInvoices();
        this.Closed += (s, e) => CleanupTempFiles();

        PopulateItems();

        TxtSubtotal.Text = $"₹{_receipt.SubTotal:N0}";
        if (_receipt.Discount > 0)
        {
            DiscountRow.Visibility = Visibility.Visible;
            TxtDiscount.Text = $"-₹{_receipt.Discount:N0}";
        }
        else
        {
            DiscountRow.Visibility = Visibility.Collapsed;
        }

        TxtGrandTotal.Text = $"₹{_receipt.GrandTotal:N0}";
        TxtPaymentMode.Text = (_receipt.PaymentMode ?? "CASH").ToUpperInvariant();

        if (string.Equals(_receipt.PaymentMode, "UPI", StringComparison.OrdinalIgnoreCase))
        {
            PaymentModeBadge.Background = new SolidColorBrush(Color.FromArgb(30, 37, 99, 235));
            TxtPaymentMode.Foreground = new SolidColorBrush(Color.FromArgb(255, 37, 99, 235));
        }
        else
        {
            PaymentModeBadge.Background = new SolidColorBrush(Color.FromArgb(30, 16, 185, 129));
            TxtPaymentMode.Foreground = new SolidColorBrush(Color.FromArgb(255, 5, 150, 105));
        }

        var savedFormat = AppServices.Database.GetSetting("receipt_export_format", "PDF");
        _isPdfFormat = string.Equals(savedFormat, "PDF", StringComparison.OrdinalIgnoreCase);
        UpdateFormatSwitcherUI();
    }

    private void UpdateFormatSwitcherUI()
    {
        var accentBrush = (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"];
        var textOnAccentBrush = (Brush)Application.Current.Resources["TextOnAccentFillColorPrimaryBrush"];
        var transparentBrush = new SolidColorBrush(Colors.Transparent);
        var defaultTextBrush = (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"];

        if (_isPdfFormat)
        {
            BtnFormatPdf.Background = accentBrush;
            BtnFormatPdf.Foreground = textOnAccentBrush;
            BtnFormatJpg.Background = transparentBrush;
            BtnFormatJpg.Foreground = defaultTextBrush;

            TxtWhatsAppBtn.Text = "Send PDF on WhatsApp";
            TxtSaveBtn.Text = "Save PDF";
            TxtCopyBtn.Text = "Copy PDF";
        }
        else
        {
            BtnFormatJpg.Background = accentBrush;
            BtnFormatJpg.Foreground = textOnAccentBrush;
            BtnFormatPdf.Background = transparentBrush;
            BtnFormatPdf.Foreground = defaultTextBrush;

            TxtWhatsAppBtn.Text = "Send Image on WhatsApp";
            TxtSaveBtn.Text = "Save JPG";
            TxtCopyBtn.Text = "Copy Image";
        }
    }

    private void FormatJpg_Click(object sender, RoutedEventArgs e)
    {
        _isPdfFormat = false;
        AppServices.Database.SetSetting("receipt_export_format", "JPG");
        UpdateFormatSwitcherUI();
    }

    private void FormatPdf_Click(object sender, RoutedEventArgs e)
    {
        _isPdfFormat = true;
        AppServices.Database.SetSetting("receipt_export_format", "PDF");
        UpdateFormatSwitcherUI();
    }

    private void PopulateItems()
    {
        ItemsContainer.Children.Clear();

        if (_receipt.Items != null && _receipt.Items.Count > 0)
        {
            for (int i = 0; i < _receipt.Items.Count; i++)
            {
                var item = _receipt.Items[i];
                var grid = new Grid
                {
                    ColumnSpacing = 6,
                    Margin = new Thickness(0, 2, 0, 2)
                };
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(32) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(48) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(58) });

                var numBlock = new TextBlock
                {
                    Text = (i + 1).ToString(),
                    FontSize = 10.5,
                    Foreground = new SolidColorBrush(Color.FromArgb(255, 148, 163, 184)),
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(numBlock, 0);

                var nameBlock = new TextBlock
                {
                    Text = item.Name,
                    FontSize = 11,
                    FontWeight = Microsoft.UI.Text.FontWeights.Medium,
                    Foreground = new SolidColorBrush(Color.FromArgb(255, 15, 23, 42)),
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(nameBlock, 1);

                var qtyBlock = new TextBlock
                {
                    Text = item.Quantity.ToString(),
                    FontSize = 11,
                    Width = 32,
                    TextAlignment = TextAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = new SolidColorBrush(Color.FromArgb(255, 100, 116, 139))
                };
                Grid.SetColumn(qtyBlock, 2);

                var rateBlock = new TextBlock
                {
                    Text = $"₹{item.Rate:N0}",
                    FontSize = 11,
                    Width = 48,
                    TextAlignment = TextAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = new SolidColorBrush(Color.FromArgb(255, 100, 116, 139))
                };
                Grid.SetColumn(rateBlock, 3);

                var totalBlock = new TextBlock
                {
                    Text = $"₹{item.Total:N0}",
                    FontSize = 11,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromArgb(255, 15, 23, 42)),
                    Width = 58,
                    TextAlignment = TextAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(totalBlock, 4);

                grid.Children.Add(numBlock);
                grid.Children.Add(nameBlock);
                grid.Children.Add(qtyBlock);
                grid.Children.Add(rateBlock);
                grid.Children.Add(totalBlock);

                ItemsContainer.Children.Add(grid);
            }
        }
        else if (!string.IsNullOrWhiteSpace(_receipt.ItemsSummary))
        {
            var summaryBlock = new TextBlock
            {
                Text = _receipt.ItemsSummary,
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(Color.FromArgb(255, 100, 116, 139))
            };
            ItemsContainer.Children.Add(summaryBlock);
        }
    }

    private string BuildFormattedReceiptText()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"INVOICE - {_shopName}");
        if (!string.IsNullOrWhiteSpace(_shopAddress))
        {
            sb.AppendLine(_shopAddress);
        }
        if (!string.IsNullOrWhiteSpace(_shopContact))
        {
            sb.AppendLine($"Phone: {_shopContact}");
        }
        sb.AppendLine("--------------------------------");
        sb.AppendLine($"Invoice: {_receipt.InvoiceNo}");
        sb.AppendLine($"Date: {_receipt.PaymentDate:dd-MMM-yyyy hh:mm tt}");
        sb.AppendLine($"Customer: {_receipt.CustomerName}");
        if (!string.IsNullOrWhiteSpace(_receipt.CustomerMobile))
        {
            sb.AppendLine($"Phone: {_receipt.CustomerMobile}");
        }
        if (!string.IsNullOrWhiteSpace(_receipt.CustomerAddress))
        {
            sb.AppendLine($"Address: {_receipt.CustomerAddress.Trim()}");
        }
        sb.AppendLine("--------------------------------");
        sb.AppendLine("ITEMS:");

        if (_receipt.Items != null && _receipt.Items.Count > 0)
        {
            for (int i = 0; i < _receipt.Items.Count; i++)
            {
                var it = _receipt.Items[i];
                sb.AppendLine($"{i + 1}. {it.Name} x{it.Quantity} @ Rs. {it.Rate:N0} = Rs. {it.Total:N0}");
            }
        }
        else if (!string.IsNullOrWhiteSpace(_receipt.ItemsSummary))
        {
            sb.AppendLine(_receipt.ItemsSummary);
        }

        sb.AppendLine("--------------------------------");
        sb.AppendLine($"Subtotal: Rs. {_receipt.SubTotal:N0}");
        if (_receipt.Discount > 0)
        {
            sb.AppendLine($"Less Discount: -Rs. {_receipt.Discount:N0}");
        }
        sb.AppendLine($"TOTAL PAID: Rs. {_receipt.GrandTotal:N0} ({_receipt.PaymentMode})");
        sb.AppendLine("--------------------------------");
        sb.AppendLine("Thank you for your visit.");

        return sb.ToString();
    }

    private async Task<string> GenerateReceiptJpgAsync()
    {
        if (!string.IsNullOrWhiteSpace(_cachedJpgPath) && File.Exists(_cachedJpgPath))
        {
            return _cachedJpgPath;
        }

        var rtb = new RenderTargetBitmap();
        int targetW = (int)Math.Round(ReceiptCard.ActualWidth * 3);
        int targetH = (int)Math.Round(ReceiptCard.ActualHeight * 3);
        if (targetW > 0 && targetH > 0)
        {
            await rtb.RenderAsync(ReceiptCard, targetW, targetH);
        }
        else
        {
            await rtb.RenderAsync(ReceiptCard);
        }

        var pixelBuffer = await rtb.GetPixelsAsync();
        var width = (uint)rtb.PixelWidth;
        var height = (uint)rtb.PixelHeight;

        var tempDir = GetTempInvoicesDirectory();
        var safeInvoice = string.Concat(_receipt.InvoiceNo.Split(Path.GetInvalidFileNameChars()));
        if (string.IsNullOrWhiteSpace(safeInvoice)) safeInvoice = "Invoice";
        var fileName = $"invoice_{safeInvoice}.jpg";

        var storageFolder = await StorageFolder.GetFolderFromPathAsync(tempDir);
        var storageFile = await storageFolder.CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting);

        using (var stream = await storageFile.OpenAsync(FileAccessMode.ReadWrite))
        {
            var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, stream);
            encoder.SetPixelData(
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Premultiplied,
                width,
                height,
                300,
                300,
                pixelBuffer.ToArray()
            );
            await encoder.FlushAsync();
        }

        _cachedJpgPath = storageFile.Path;
        return _cachedJpgPath;
    }

    private async Task<string> GenerateReceiptPdfAsync()
    {
        if (!string.IsNullOrWhiteSpace(_cachedPdfPath) && File.Exists(_cachedPdfPath))
        {
            return _cachedPdfPath;
        }

        return await Task.Run(() =>
        {
            var tempDir = GetTempInvoicesDirectory();
            var safeInvoice = string.Concat(_receipt.InvoiceNo.Split(Path.GetInvalidFileNameChars()));
            if (string.IsNullOrWhiteSpace(safeInvoice)) safeInvoice = "Invoice";
            var fileName = $"invoice_{safeInvoice}.pdf";
            var pdfPath = Path.Combine(tempDir, fileName);

            InvoicePdfBuilder.GenerateA4InvoicePdf(_receipt, _shopName, _shopAddress, _shopContact, pdfPath);
            _cachedPdfPath = pdfPath;
            return pdfPath;
        });
    }

    private static string GetTempInvoicesDirectory()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "SevaDesk", "TempInvoices");
        Directory.CreateDirectory(tempDir);
        return tempDir;
    }

    private void CleanupTempFiles()
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(_cachedJpgPath) && File.Exists(_cachedJpgPath))
            {
                File.Delete(_cachedJpgPath);
                _cachedJpgPath = null;
            }
        }
        catch { }

        try
        {
            if (!string.IsNullOrWhiteSpace(_cachedPdfPath) && File.Exists(_cachedPdfPath))
            {
                File.Delete(_cachedPdfPath);
                _cachedPdfPath = null;
            }
        }
        catch { }

        PurgeOldTempInvoices();
    }

    private static void PurgeOldTempInvoices()
    {
        try
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "SevaDesk", "TempInvoices");
            if (Directory.Exists(tempDir))
            {
                var dirInfo = new DirectoryInfo(tempDir);
                foreach (var file in dirInfo.GetFiles())
                {
                    if (DateTime.UtcNow - file.CreationTimeUtc > TimeSpan.FromMinutes(15))
                    {
                        try { file.Delete(); } catch { }
                    }
                }
            }
        }
        catch { }
    }

    private async Task CopyFileToClipboardAsync(string filePath, bool isPdf)
    {
        var storageFile = await StorageFile.GetFileFromPathAsync(filePath);
        var dataPackage = new DataPackage
        {
            RequestedOperation = DataPackageOperation.Copy
        };

        if (!isPdf)
        {
            dataPackage.SetBitmap(RandomAccessStreamReference.CreateFromFile(storageFile));
        }

        dataPackage.SetStorageItems(new[] { storageFile });
        Clipboard.SetContent(dataPackage);
    }

    private void ShowStatus(string message, bool isSuccess)
    {
        StatusBanner.Visibility = Visibility.Visible;
        TxtActionStatus.Text = message;
        if (isSuccess)
        {
            StatusBanner.Background = new SolidColorBrush(Color.FromArgb(25, 16, 185, 129));
            StatusBanner.BorderBrush = new SolidColorBrush(Color.FromArgb(80, 16, 185, 129));
            IconStatus.Glyph = "\uE73E";
            IconStatus.Foreground = new SolidColorBrush(Color.FromArgb(255, 16, 185, 129));
            TxtActionStatus.Foreground = new SolidColorBrush(Color.FromArgb(255, 6, 95, 70));
        }
        else
        {
            StatusBanner.Background = new SolidColorBrush(Color.FromArgb(25, 239, 68, 68));
            StatusBanner.BorderBrush = new SolidColorBrush(Color.FromArgb(80, 239, 68, 68));
            IconStatus.Glyph = "\uE783";
            IconStatus.Foreground = new SolidColorBrush(Color.FromArgb(255, 239, 68, 68));
            TxtActionStatus.Foreground = new SolidColorBrush(Color.FromArgb(255, 185, 28, 28));
        }
    }

    private async void SendWhatsApp_Click(object sender, RoutedEventArgs e)
    {
        var rawPhone = TxtMobileInput.Text?.Trim() ?? string.Empty;
        var digitsOnly = new string(rawPhone.Where(char.IsDigit).ToArray());

        if (digitsOnly.Length < 10)
        {
            ShowStatus("Please enter a 10-digit mobile number to send on WhatsApp.", false);
            TxtMobileInput.Focus(FocusState.Programmatic);
            return;
        }

        string fullPhone = digitsOnly.Length == 10 ? "91" + digitsOnly : digitsOnly;

        try
        {
            string formatName;
            if (_isPdfFormat)
            {
                var pdfPath = await GenerateReceiptPdfAsync();
                await CopyFileToClipboardAsync(pdfPath, isPdf: true);
                formatName = "PDF";
            }
            else
            {
                var jpgPath = await GenerateReceiptJpgAsync();
                await CopyFileToClipboardAsync(jpgPath, isPdf: false);
                formatName = "image";
            }

            var appUri = new Uri($"whatsapp://send?phone={fullPhone}");
            var launchedApp = await Windows.System.Launcher.LaunchUriAsync(appUri);

            if (!launchedApp)
            {
                var webUri = new Uri($"https://wa.me/{fullPhone}");
                await Windows.System.Launcher.LaunchUriAsync(webUri);
            }

            ShowStatus($"Invoice {formatName} copied to clipboard. Chat opened — press Ctrl+V in WhatsApp to send.", true);
        }
        catch (Exception ex)
        {
            ShowStatus($"Could not prepare invoice: {ex.Message}", false);
        }
    }

    private async void SaveFile_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string sourcePath;
            string? destinationPath;
            var safeInvoice = string.Concat(_receipt.InvoiceNo.Split(Path.GetInvalidFileNameChars()));
            if (string.IsNullOrWhiteSpace(safeInvoice)) safeInvoice = "Invoice";

            if (_isPdfFormat)
            {
                sourcePath = await GenerateReceiptPdfAsync();
                destinationPath = await AppServices.Pickers.PickSaveFileAsync($"{safeInvoice}.pdf", ".pdf", "PDF Document (*.pdf)");
            }
            else
            {
                sourcePath = await GenerateReceiptJpgAsync();
                destinationPath = await AppServices.Pickers.PickSaveFileAsync($"{safeInvoice}.jpg", ".jpg", "JPEG Image (*.jpg)");
            }

            if (!string.IsNullOrWhiteSpace(destinationPath))
            {
                File.Copy(sourcePath, destinationPath, overwrite: true);
                ShowStatus($"Invoice saved to {Path.GetFileName(destinationPath)}", true);
            }
        }
        catch (Exception ex)
        {
            ShowStatus($"Could not save invoice: {ex.Message}", false);
        }
    }

    private async void CopySlip_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_isPdfFormat)
            {
                var pdfPath = await GenerateReceiptPdfAsync();
                await CopyFileToClipboardAsync(pdfPath, isPdf: true);
                ShowStatus("Invoice PDF copied to clipboard. Ready to paste (Ctrl+V).", true);
            }
            else
            {
                var jpgPath = await GenerateReceiptJpgAsync();
                await CopyFileToClipboardAsync(jpgPath, isPdf: false);
                ShowStatus("Invoice image copied to clipboard. Ready to paste (Ctrl+V).", true);
            }
        }
        catch (Exception ex)
        {
            ShowStatus($"Could not copy invoice: {ex.Message}", false);
        }
    }

    private async void PrintReceipt_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var pdfPath = await GenerateReceiptPdfAsync();
            var storageFile = await StorageFile.GetFileFromPathAsync(pdfPath);
            var success = await Windows.System.Launcher.LaunchFileAsync(storageFile);
            if (success)
            {
                ShowStatus("Invoice opened in PDF viewer for printing.", true);
            }
            else
            {
                ShowStatus("Could not launch default PDF viewer.", false);
            }
        }
        catch (Exception ex)
        {
            ShowStatus($"Could not open invoice: {ex.Message}", false);
        }
    }
}
