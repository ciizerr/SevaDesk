using System;
using CommunityToolkit.Mvvm.ComponentModel;
using SevaDesk.Core.Models;

namespace SevaDesk_App.ViewModels.Pages;

public partial class CustomerTimelineItemModel : ObservableObject
{
    public bool IsPayment { get; }
    public Payment? Payment { get; }
    public CustomerSessionRowModel? SessionRow { get; }
    public DateTime Timestamp { get; }

    public string Title { get; }
    public string FormattedDate => Timestamp.ToLocalTime().ToString("dd MMM yyyy, hh:mm tt");
    public string FormattedTime => Timestamp.ToLocalTime().ToString("hh:mm tt");
    public string Glyph => IsPayment ? "\uE8C7" : "\uE768";
    public string GlyphColor => IsPayment ? "#10B981" : "#0078D4";
    public string SecondaryInfo { get; }
    public string TagText { get; }

    public CustomerTimelineItemModel(Payment payment)
    {
        IsPayment = true;
        Payment = payment;
        Timestamp = payment.PaymentDate;
        Title = !string.IsNullOrWhiteSpace(payment.InvoiceNo) ? $"Receipt {payment.InvoiceNo}" : "Counter Bill";
        SecondaryInfo = !string.IsNullOrWhiteSpace(payment.ItemsSummary) ? payment.ItemsSummary : (!string.IsNullOrWhiteSpace(payment.Notes) ? payment.Notes : "Direct Payment");
        TagText = $"₹{payment.Amount:N0} ({payment.PaymentMethod})";
    }

    public CustomerTimelineItemModel(CustomerSessionRowModel sessionRow)
    {
        IsPayment = false;
        SessionRow = sessionRow;
        Timestamp = sessionRow.Session.StartedAt;
        Title = $"Counter Session ({sessionRow.FormattedDuration})";
        SecondaryInfo = !string.IsNullOrWhiteSpace(sessionRow.Session.Notes) ? sessionRow.Session.Notes : sessionRow.WorkSummary;
        TagText = sessionRow.HasPayment ? $"Paid ₹{sessionRow.Payment!.Amount:N0}" : "Unbilled";
    }
}
