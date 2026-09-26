using CommunityToolkit.Mvvm.ComponentModel;

namespace SevaDesk.Core.Models;

public class ApplicationItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string? CustomerId { get; set; }
    public string? SessionId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty; // e.g. SSC CGL 2026, PAN Application
    public string PortalName { get; set; } = string.Empty; // e.g. ssc.gov.in, NSDL
    public string ApplicationNumber { get; set; } = string.Empty;
    public string Status { get; set; } = "Draft"; // Draft, Docs Ready, Completed
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

public partial class ApplicationChecklistItem : ObservableObject
{
    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private bool _isCompleted;

    [ObservableProperty]
    private string _category = "Document";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMatchedFile))]
    private string? _matchedFileName;

    public bool HasMatchedFile => !string.IsNullOrWhiteSpace(MatchedFileName);
}
