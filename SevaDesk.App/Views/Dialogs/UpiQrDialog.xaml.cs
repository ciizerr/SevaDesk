using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SevaDesk_App.Services;
using Windows.ApplicationModel.DataTransfer;

namespace SevaDesk_App.Views.Dialogs;

public sealed partial class UpiQrDialog : ContentDialog
{
    private readonly string _vpa;
    private readonly string _payeeName;
    private readonly decimal _amount;
    private readonly string _customerName;

    public UpiQrDialog(string vpa, string payeeName, decimal amount, string customerName)
    {
        InitializeComponent();
        this.EnableLightDismiss();

        _vpa = string.IsNullOrWhiteSpace(vpa) ? "sevadesk.csc@upi" : vpa.Trim();
        _payeeName = string.IsNullOrWhiteSpace(payeeName) ? "SevaDesk Cyber Center" : payeeName.Trim();
        _amount = amount;
        _customerName = string.IsNullOrWhiteSpace(customerName) ? "Walk-in Customer" : customerName.Trim();

        TxtAmount.Text = _amount > 0 ? $"₹{_amount:N0}" : "Any Amount";
        TxtPayee.Text = _payeeName;
        TxtCustomer.Text = $"Customer: {_customerName}";
        TxtVpa.Text = _vpa;

        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            var note = _amount > 0 ? $"Bill for {_customerName}" : "Cyber Cafe Services";
            var payload = AppServices.QrCode.BuildUpiPayload(_vpa, _payeeName, _amount, note);
            var bitmap = AppServices.QrCode.GenerateQrBitmap(payload, pixelsPerModule: 10);
            if (bitmap != null)
            {
                QrImage.Source = bitmap;
            }
        }
        catch { }
        finally
        {
            LoadingRing.IsActive = false;
            LoadingRing.Visibility = Visibility.Collapsed;
        }
    }

    private void CopyVpa_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dataPackage = new DataPackage();
            dataPackage.SetText(_vpa);
            Clipboard.SetContent(dataPackage);
        }
        catch { }
    }
}
