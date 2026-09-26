using System;
using CommunityToolkit.Mvvm.ComponentModel;
using SevaDesk.Core.Models;

namespace SevaDesk_App.ViewModels.Pages;

public partial class CustomerSessionRowModel : ObservableObject
{
    public Session Session { get; }
    public Payment? Payment { get; }

    [ObservableProperty]
    private bool _isHighlighted;

    public List<BilledServiceItem> BilledItems { get; }
    public bool HasBilledItems => BilledItems.Count > 0;

    public CustomerSessionRowModel(Session session, Payment? payment = null)
    {
        Session = session;
        Payment = payment;

        var parsed = BilledServiceItem.ParseSummary(payment?.ItemsSummary);
        if (parsed.Count == 0)
        {
            if (!string.IsNullOrWhiteSpace(session.Notes))
            {
                parsed.Add(new BilledServiceItem
                {
                    ServiceName = session.Notes,
                    Glyph = "\uE8A5"
                });
            }
            else
            {
                parsed.Add(new BilledServiceItem
                {
                    ServiceName = "General Desk Session",
                    Glyph = "\uE7BE"
                });
            }
        }
        BilledItems = parsed;
    }

    public bool HasPayment => Payment != null;

    public string FormattedDate => Session.StartedAt.ToLocalTime().ToString("dd MMM yyyy, hh:mm tt");

    public string FormattedDateShort => Session.StartedAt.ToLocalTime().ToString("dd MMM yyyy");

    public string FormattedTime => Session.StartedAt.ToLocalTime().ToString("hh:mm tt");

    public string FormattedDuration => Session.FormattedDuration;

    public string Status => Session.Status;

    public string WorkSummary
    {
        get
        {
            if (Payment != null && !string.IsNullOrWhiteSpace(Payment.ItemsSummary))
            {
                return Payment.ItemsSummary;
            }
            if (!string.IsNullOrWhiteSpace(Session.Notes))
            {
                return Session.Notes;
            }
            return "General Desk Session";
        }
    }

    public string AmountText => Payment != null ? $"₹{Payment.Amount:N0}" : "Unbilled";

    public string FormattedAmount => Payment != null ? $"₹{Payment.Amount:N0}" : string.Empty;

    public string PaymentMethod => Payment?.PaymentMethod ?? string.Empty;

    public string InvoiceNo => Payment?.InvoiceNo ?? string.Empty;

    public bool HasNotes => !string.IsNullOrWhiteSpace(Session.Notes);

    public string Notes => Session.Notes ?? string.Empty;
}
