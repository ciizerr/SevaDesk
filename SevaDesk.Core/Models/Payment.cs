namespace SevaDesk.Core.Models;

public class Payment
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string InvoiceNo { get; set; } = string.Empty;
    public string? CustomerId { get; set; }
    public string CustomerName { get; set; } = "Walk-in Customer";
    public string? SessionId { get; set; }
    public decimal Amount { get; set; }
    public string PaymentMethod { get; set; } = "UPI"; // UPI, Cash
    public string? ReferenceNumber { get; set; }
    public DateTime PaymentDate { get; set; } = DateTime.UtcNow;
    public string? ItemsSummary { get; set; }
    public string? Notes { get; set; }
}
