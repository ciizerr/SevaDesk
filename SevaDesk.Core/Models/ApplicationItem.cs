namespace SevaDesk.Core.Models;

public class ApplicationItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string? CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty; // e.g. SSC CGL 2026, PAN Application
    public string PortalName { get; set; } = string.Empty; // e.g. ssc.gov.in, NSDL
    public string ApplicationNumber { get; set; } = string.Empty;
    public string Status { get; set; } = "Draft"; // Draft, Docs Uploaded, Submitted, Completed
    public decimal ServiceCharge { get; set; }
    public decimal GovtFee { get; set; }
    public decimal TotalAmount => ServiceCharge + GovtFee;
    public string FormattedTotal => $"₹{TotalAmount:N0}";
    public string FormattedServiceCharge => $"₹{ServiceCharge:N0}";
    public string FormattedGovtFee => $"₹{GovtFee:N0}";
    public string RequiredDocs { get; set; } = "Photo, Signature, Aadhaar, Marksheet";
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}

public class ApplicationChecklistItem
{
    public string Title { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }
    public string Category { get; set; } = "Document";
}
