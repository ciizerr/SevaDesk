namespace SevaDesk.Core.Models;

public class Payment
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string InvoiceNo { get; set; } = string.Empty;
    public string? CustomerId { get; set; }
    public string CustomerName { get; set; } = "Walk-in Customer";
    public string? SessionId { get; set; }
    public decimal SubTotal { get; set; }
    public decimal Discount { get; set; }
    public decimal Amount { get; set; }
    public string PaymentMethod { get; set; } = "UPI"; // UPI, Cash
    public string? ReferenceNumber { get; set; }
    public DateTime PaymentDate { get; set; } = DateTime.UtcNow;
    public string? ItemsSummary { get; set; }
    public string? Notes { get; set; }
    public string Status { get; set; } = "Paid"; // Paid, Pending
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsPending => string.Equals(Status, "Pending", StringComparison.OrdinalIgnoreCase);

    private List<BilledServiceItem>? _billedItems;
    [System.Text.Json.Serialization.JsonIgnore]
    public List<BilledServiceItem> BilledItems
    {
        get
        {
            if (_billedItems != null) return _billedItems;
            var list = BilledServiceItem.ParseSummary(ItemsSummary);
            if (list.Count == 0)
            {
                if (!string.IsNullOrWhiteSpace(Notes))
                {
                    list.Add(new BilledServiceItem
                    {
                        ServiceName = Notes,
                        Glyph = "\uE8A5"
                    });
                }
                else
                {
                    list.Add(new BilledServiceItem
                    {
                        ServiceName = "Direct Billing",
                        Glyph = "\uE8C7"
                    });
                }
            }
            _billedItems = list;
            return _billedItems;
        }
    }
}
